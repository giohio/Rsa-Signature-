using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Org.BouncyCastle.Security;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using RsaSignApi.Data;
using RsaSignApi.Utils;
using RsaSignApi.Model.RequestModels;
using RsaSignApi.Services;
using System.Text;
using System.Collections.Generic;
using RsaSignApi.Model;
using Microsoft.Extensions.Logging;

namespace RsaSignApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SignController : ControllerBase
    {
        private readonly ISignService _signService;
        private readonly ILogger<SignController> _logger;
        
        public SignController(ISignService signService, ILogger<SignController> logger)
        {
            _signService = signService;
            _logger = logger;
        }

        // 1. GenerateKeys (unchanged)...
        [HttpPost("generate-keys")]
        public async Task<IActionResult> GenerateKeys([FromBody] GenerateKeyModel model)
        {
            var result = await _signService.GenerateKeysAsync(model);
            if (!result.Success)
                return BadRequest($"Lỗi: {result.Message}");
                
            // Return both public and private keys for frontend display, and signal that the key has been saved
            return Ok(new { 
                message = $"Thành công: {result.Message}", 
                publicKey = result.PublicKey,
                privateKey = result.PrivateKey, // Add private key
                signId = result.SignId, 
                signatureName = model.SignatureName, 
                signatureType = $"Rsa{model.KeySize}", 
                isActive = true,
                keyPair = new {
                    publicKey = result.PublicKey,
                    privateKey = result.PrivateKey
                }
            });
        }

        // 2. GetSignatures (unchanged)...
        [HttpGet("list/{userId}")]
        public async Task<IActionResult> GetSignatures(string userId)
        {
            var result = await _signService.GetSignaturesAsync(userId);
            if (!result.Success)
                return NotFound($"Không tìm thấy: {result.Message}");
            return Ok(new { message = result.Message, signatures = result.Signatures });
        }

        // 3. DeleteSignature (unchanged)...
        [HttpDelete("delete/{userId}/{signId}")]
        public async Task<IActionResult> DeleteSignature(string userId, string signId)
        {
            var result = await _signService.DeleteSignatureAsync(userId, signId);
            if (!result.Success)
                return NotFound($"Không tìm thấy: {result.Message}");
            return Ok(new { message = result.Message });
        }

        // 4. SignDocument: PDF trực tiếp, else convert→PDF rồi ký
        [HttpPost("sign-document")]
        public async Task<IActionResult> SignDocument([FromForm] SignDocumentModel model)
        {
            // Log all form values for debugging
            _logger.LogInformation("SignDocument form data:");
            foreach (var key in Request.Form.Keys)
            {
                _logger.LogInformation($"Form key: {key}, Value: {Request.Form[key]}");
            }
            
            // Try to get values from form if model binding failed
            if (string.IsNullOrEmpty(model.UserId))
            {
                if (Request.Form.ContainsKey("userId"))
                    model.UserId = Request.Form["userId"];
            }
            
            if (string.IsNullOrEmpty(model.SignId))
            {
                if (Request.Form.ContainsKey("signId"))
                    model.SignId = Request.Form["signId"];
            }
            
            if (Request.Form.ContainsKey("useEmbeddedSign"))
            {
                bool.TryParse(Request.Form["useEmbeddedSign"], out bool useEmbedded);
                model.UseEmbeddedSign = useEmbedded;
            }
            
            if (Request.Form.ContainsKey("hashAlgorithm"))
            {
                model.HashAlgorithm = Request.Form["hashAlgorithm"];
            }
            
            _logger.LogInformation($"After form processing: UserId={model.UserId}, SignId={model.SignId}, UseEmbeddedSign={model.UseEmbeddedSign}, HashAlgorithm={model.HashAlgorithm}");
            _logger.LogInformation($"File details: Name={model.File?.FileName}, Length={model.File?.Length}");
            
            // Check for required fields manually
            if (string.IsNullOrEmpty(model.UserId))
            {
                _logger.LogError("Mã người dùng bị thiếu hoặc trống");
                return BadRequest("Thiếu mã người dùng");
            }
            
            if (string.IsNullOrEmpty(model.SignId))
            {
                _logger.LogError("Mã chữ ký bị thiếu hoặc trống");
                return BadRequest("Thiếu mã chữ ký");
            }
            
            if (model.File == null || model.File.Length == 0)
            {
                _logger.LogError("Tệp bị thiếu hoặc trống");
                return BadRequest("Thiếu tệp để ký");
            }
            
            try
            {
                var result = await _signService.SignDocumentAsync(model);
                if (!result.Success)
                {
                    _logger.LogError($"SignDocument failed: {result.Message}");
                    return BadRequest($"Lỗi ký tài liệu: {result.Message}");
                }
                
                _logger.LogInformation($"SignDocument succeeded, returning signed file: {result.FileName}");
                return File(result.SignedFile, "application/pdf", result.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"SignDocument exception: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return BadRequest($"Lỗi: {ex.Message}");
            }
        }

        // ----- Helpers -----


        // 5. VerifySignature: Support both embedded and detached signatures
        [HttpPost("verify-signature")]
        public async Task<IActionResult> VerifySignature([FromForm] VerifySignatureModel model)
        {
            // Log form data for debugging
            _logger.LogInformation("VerifySignature form data:");
            foreach (var key in Request.Form.Keys)
            {
                _logger.LogInformation($"Form key: {key}, Value: {Request.Form[key]}");
            }
            
            // Try to get values from form if model binding failed
            if (Request.Form.ContainsKey("isEmbedded"))
            {
                bool.TryParse(Request.Form["isEmbedded"], out bool isEmbedded);
                model.IsEmbedded = isEmbedded;
            }
            
            _logger.LogInformation($"After form processing: IsEmbedded={model.IsEmbedded}");
            _logger.LogInformation($"File details: Name={model.File?.FileName}, Length={model.File?.Length}");
            if (!model.IsEmbedded)
            {
                _logger.LogInformation($"Original file details: Name={model.OriginalFile?.FileName}, Length={model.OriginalFile?.Length}");
            }
            
            if (model.File == null) 
            {
                _logger.LogError("Tệp bị thiếu hoặc trống");
                return BadRequest("Thiếu tệp chữ ký");
            }
            
            if (!model.IsEmbedded && model.OriginalFile == null)
            {
                _logger.LogError("Tệp gốc là bắt buộc để xác thực chữ ký tách rời");
                return BadRequest("Thiếu tệp gốc để xác thực chữ ký tách rời");
            }
            
            try
            {
                var result = await _signService.VerifySignatureAsync(
                    file: model.File,
                    originalFile: model.OriginalFile,
                    isEmbedded: model.IsEmbedded);
                    
                if (!result.Success)
                {
                    _logger.LogError($"VerifySignature failed: {result.Message}");
                    return BadRequest($"Lỗi xác thực: {result.Message}");
                }
                
                _logger.LogInformation($"VerifySignature result: Valid={result.IsValid}, FullName={result.FullName}, Email={result.Email}");
                return Ok(new
                {
                    isValid = result.IsValid,
                    message = $"Kết quả: {result.Message}",
                    fullName = result.FullName,
                    email = result.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"VerifySignature exception: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return BadRequest($"Lỗi: {ex.Message}");
            }
        }
    }
}
