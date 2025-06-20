using Microsoft.AspNetCore.Mvc;
using RsaSignApi.Model.RequestModels;
using RsaSignApi.Services;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;
using System.Numerics;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace RsaSignApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ManualSignController : ControllerBase
    {
        private readonly IManualSignService _manualSignService;

        public ManualSignController(IManualSignService manualSignService)
        {
            _manualSignService = manualSignService;
        }

        #region Key Management

        [HttpPost("generate-ed")]
        public async Task<IActionResult> GenerateED([FromBody] GenerateEDFromPQModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _manualSignService.GenerateEDFromPQAsync(model);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new
            {
                message = result.Message,
                e = result.E,
                d = result.D
            });
        }

        [HttpPost("generate-keypair")]
        public async Task<IActionResult> GenerateKeyPair([FromBody] ManualSignParamsModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _manualSignService.GenerateKeyPairFromParamsAsync(model);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new
            {
                message = result.Message,
                publicKey = result.PublicKey,
                privateKey = result.PrivateKey
            });
        }

        [HttpPost("save-key-pair")]
        public async Task<IActionResult> SaveKeyPair([FromBody] SaveKeyPairModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _manualSignService.SaveKeyPairAsync(model);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new
            {
                message = result.Message,
                signId = result.SignId
            });
        }

        [HttpGet("get-key-details/{userId}/{signId}")]
        public async Task<IActionResult> GetKeyDetails(string userId, string signId)
        {
            var result = await _manualSignService.GetKeyDetailsAsync(userId, signId);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new { message = result.Message, signature = result.Signature });
        }

        [HttpPut("update-key-pair")]
        public async Task<IActionResult> UpdateKeyPair([FromBody] UpdateKeyPairModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _manualSignService.UpdateKeyPairAsync(model);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new { message = result.Message, signId = result.SignId });
        }

        [HttpPost("generate-params")]
        public async Task<IActionResult> GenerateParams([FromQuery] int keySize = 2048, [FromQuery] bool isEducationalMode = false)
        {
            // Validate key size based on mode
            if (isEducationalMode)
            {
                if (keySize > 100)
                {
                    keySize = 100; // Limit to 100 for educational mode
                }
            }
            else
            {
                if (keySize < 2048)
                {
                    return BadRequest(new { error = "Kích thước khóa phải ít nhất 2048 bit cho chế độ sản xuất" });
                }
            }

            var result = await _manualSignService.GenerateParamsAsync(keySize, isEducationalMode);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new
            {
                message = result.Message,
                p = result.P,
                q = result.Q,
                e = result.E,
                d = result.D,
                isEducationalMode = isEducationalMode
            });
        }

        #endregion

        #region Key Import/Export

        [HttpPost("import-keys")]
        public async Task<IActionResult> ImportKeys([FromBody] ManualImportKeysModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                Console.WriteLine($"Received import keys request for user: {model.UserId}, signature name: {model.SignatureName}");
                
                // Validate the request
                if (string.IsNullOrEmpty(model.KeyFileContent))
                {
                    return BadRequest(new { error = "Nội dung tệp khóa là bắt buộc" });
                }
                
                if (string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest(new { error = "Mã người dùng là bắt buộc" });
                }
                
                if (string.IsNullOrEmpty(model.SignatureName))
                {
                    return BadRequest(new { error = "Tên chữ ký là bắt buộc" });
                }

            var result = await _manualSignService.ImportKeysFromFilesAsync(model);
            if (!result.Success)
                {
                    Console.WriteLine($"Import keys failed: {result.Message}");
                return BadRequest(new { error = result.Message });
                }

                Console.WriteLine("Keys imported successfully, returning response");
            return Ok(new
            {
                message = result.Message,
                publicKey = result.PublicKey,
                privateKey = result.PrivateKey
            });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ImportKeys: {ex.Message}");
                return BadRequest(new { error = $"Lỗi nhập khóa: {ex.Message}" });
            }
        }

        [HttpPost("export-keys")]
        public async Task<IActionResult> ExportKeys([FromBody] ManualExportKeysModel model)
        {
            if (string.IsNullOrEmpty(model.UserId) || string.IsNullOrEmpty(model.SignId))
                return BadRequest(new { error = "Mã người dùng và Mã chữ ký là bắt buộc" });

            try
            {
                // Get the signature record from the database
                var keyResult = await _manualSignService.GetKeyDetailsAsync(model.UserId, model.SignId);
                if (!keyResult.Success || keyResult.Signature == null)
                {
                    return BadRequest(new { error = keyResult.Message });
                }
                
                var signature = keyResult.Signature;

                // Create the export object
                var exportObj = new Dictionary<string, object>
                {
                    { "signatureName", signature.SignatureName ?? "Exported Signature" },
                    { "signatureType", signature.SignatureType ?? "Manual" },
                    { "exportDate", DateTime.UtcNow.ToString("o") }
                };

                // Add the keys
                if (!string.IsNullOrEmpty(signature.PublicKey))
                {
                    exportObj["publicKey"] = signature.PublicKey;
                }

                if (!string.IsNullOrEmpty(signature.PrivateKey))
                {
                    exportObj["privateKey"] = signature.PrivateKey;
                }

                // Add p, q, e, d if available
                if (!string.IsNullOrEmpty(signature.P))
                {
                    exportObj["p"] = signature.P;
                }

                if (!string.IsNullOrEmpty(signature.Q))
                {
                    exportObj["q"] = signature.Q;
                }

                if (!string.IsNullOrEmpty(signature.E))
                {
                    exportObj["e"] = signature.E;
                }

                if (!string.IsNullOrEmpty(signature.D))
                {
                    exportObj["d"] = signature.D;
                }

                // Convert to JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(exportObj, jsonOptions);
                
                // Return the file
                string filename = $"{signature.SignatureName ?? "signature"}_{DateTime.UtcNow:yyyyMMdd}.json";
                return File(jsonBytes, "application/json", filename);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Lỗi xuất khóa: {ex.Message}" });
            }
        }

        #endregion

        #region Signing Operations

        [HttpPost("sign-with-ned")]
        public async Task<IActionResult> SignWithNED([FromBody] JsonElement body)
        {
            try
            {
                string n = "", e = "", d = "", data = "", hashAlgorithm = "SHA256";
                
                if (body.TryGetProperty("n", out var nElement))
                    n = nElement.GetString() ?? "";
                
                if (body.TryGetProperty("e", out var eElement))
                    e = eElement.GetString() ?? "";
                
                if (body.TryGetProperty("d", out var dElement))
                    d = dElement.GetString() ?? "";
                
                if (body.TryGetProperty("data", out var dataElement))
                    data = dataElement.GetString() ?? "";
                
                if (body.TryGetProperty("hashAlgorithm", out var hashElement))
                    hashAlgorithm = hashElement.GetString() ?? "SHA256";
                
                if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(e) || 
                    string.IsNullOrEmpty(d) || string.IsNullOrEmpty(data))
                {
                    return BadRequest(new { error = "Các tham số bắt buộc: n, e, d, data" });
                }
                
                var result = await _manualSignService.SignWithNEDAsync(n, e, d, data, hashAlgorithm);
                
                if (!result.Success)
                    return BadRequest(new { error = result.Message });
                
                return Ok(new
                {
                    message = result.Message,
                    signature = result.Signature
                });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { error = $"Lỗi xử lý yêu cầu: {ex.Message}" });
            }
        }

        [HttpPost("sign-with-private-key-rsa")]
        public async Task<IActionResult> SignWithPrivateKeyRsa([FromBody] JsonElement body)
        {
            try
            {
                string privateKeyBase64 = "", data = "", hashAlgorithm = "SHA256";
                
                if (body.TryGetProperty("privateKey", out var pkElem))
                    privateKeyBase64 = pkElem.GetString() ?? "";
                
                if (body.TryGetProperty("hashAlgorithm", out var hashElem))
                    hashAlgorithm = hashElem.GetString() ?? "SHA256";
                
                if (body.TryGetProperty("data", out var dataElem))
                    data = dataElem.GetString() ?? "";
                
                if (string.IsNullOrEmpty(privateKeyBase64) || string.IsNullOrEmpty(data))
                    return BadRequest(new { error = "Khóa riêng và dữ liệu là bắt buộc" });
                
                var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] signatureBytes;
                var algo = hashAlgorithm.ToUpperInvariant();
                
                switch (algo)
                {
                    case "MD5":
                        signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
                        break;
                    case "SHA1":
                        signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                        break;
                    case "SHA512":
                        signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
                        break;
                    case "SHA256":
                    default:
                        signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        break;
                }

                var signature = Convert.ToBase64String(signatureBytes);
                return Ok(new { message = "Ký văn bản thành công", signature });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Lỗi ký bằng khóa riêng RSA: {ex.Message}" });
            }
        }

        [HttpPost("sign-file")]
        public async Task<IActionResult> SignFile([FromForm] IFormFile file, [FromForm] string userId, [FromForm] string signatureId, [FromForm] string hashAlgorithm = "SHA256")
        {
            try
            {
                Console.WriteLine($"Received sign-file request: userId={userId}, signatureId={signatureId}, hashAlgorithm={hashAlgorithm}, file={file?.FileName}");
                
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "Tệp là bắt buộc" });

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(signatureId))
                {
                    return BadRequest(new { error = "Mã người dùng và mã chữ ký là bắt buộc" });
                }
                
                var result = await _manualSignService.SignFileAsync(
                    file,
                    userId,
                    signatureId,
                    hashAlgorithm ?? "SHA256",
                    "", // privateKey
                    "", // n
                    "", // e
                    ""  // d
                );
                
                if (!result.Success)
                    return BadRequest(new { error = result.Message });

                return Ok(new
                {
                    message = result.Message,
                    signature = result.Signature,
                    fileName = result.FileName
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in sign-file endpoint: {ex.Message}");
                return BadRequest(new { error = $"Lỗi xử lý yêu cầu: {ex.Message}" });
            }
        }

        [HttpPost("verify-file-signature")]
        public async Task<IActionResult> VerifyFileSignature(
            [FromForm] IFormFile file, 
            [FromForm] string signature, 
            [FromForm] string publicKey, 
            [FromForm] string hashAlgorithm = "SHA256")
            {
                if (file == null || file.Length == 0)
                return BadRequest(new { error = "Tệp là bắt buộc" });
                
                if (string.IsNullOrEmpty(signature))
                    return BadRequest(new { error = "Chữ ký là bắt buộc" });
                
                if (string.IsNullOrEmpty(publicKey))
                    return BadRequest(new { error = "Khóa công khai là bắt buộc" });
                
                var result = await _manualSignService.VerifyFileSignatureAsync(
                file, signature, publicKey, hashAlgorithm);
                
            return Ok(new { message = result.Message, isValid = result.Success });
            }

        [HttpPost("sign-file-and-download")]
        public async Task<IActionResult> SignFileAndDownload([FromForm] IFormFile file, [FromForm] string userId, [FromForm] string signatureId, [FromForm] string hashAlgorithm = "SHA256")
        {
            try
            {
                Console.WriteLine($"Received sign-file-and-download request: userId={userId}, signatureId={signatureId}, hashAlgorithm={hashAlgorithm}, file={file?.FileName}");
                
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "Tệp là bắt buộc" });
                
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(signatureId))
                {
                    return BadRequest(new { error = "Mã người dùng và mã chữ ký là bắt buộc" });
                }
                
                var result = await _manualSignService.SignFileAsync(
                    file,
                    userId,
                    signatureId,
                    hashAlgorithm ?? "SHA256",
                    "", // privateKey
                    "", // n
                    "", // e
                    ""  // d
                );
                
                if (!result.Success)
                    return BadRequest(new { error = result.Message });

                // For now, just return the signature in a text file
                // In a real application, you might want to create a signed version of the file
                var signatureBytes = Encoding.UTF8.GetBytes(result.Signature);
                
                return File(signatureBytes, "text/plain", $"{Path.GetFileNameWithoutExtension(file.FileName)}_signature.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in sign-file-and-download endpoint: {ex.Message}");
                return BadRequest(new { error = $"Lỗi xử lý yêu cầu: {ex.Message}" });
            }
        }

        #endregion

        [HttpGet("generate-small-primes")]
        public IActionResult GenerateSmallPrimes([FromQuery] int max = 100, [FromQuery] int count = 10)
        {
            try
            {
                // Limit the maximum value and count for performance reasons
                if (max > 1000) max = 1000;
                if (count > 50) count = 50;
                
                // Generate a list of primes up to max
                var allPrimes = new List<int>();
                for (int i = 2; i <= max; i++)
                {
                    bool isPrime = true;
                    for (int j = 2; j * j <= i; j++)
                    {
                        if (i % j == 0)
                        {
                            isPrime = false;
                            break;
                        }
                    }
                    if (isPrime)
                    {
                        allPrimes.Add(i);
                    }
                }
                
                // If we don't have enough primes, return all we found
                if (allPrimes.Count <= count)
                {
                    return Ok(new
                    {
                        message = $"Đã tạo {allPrimes.Count} số nguyên tố nhỏ hơn hoặc bằng {max}",
                        primes = allPrimes
                    });
                }
                
                // Otherwise, select a random subset
                var random = new Random();
                var selectedPrimes = new List<int>();
                var selectedIndices = new HashSet<int>();
                
                while (selectedPrimes.Count < count)
                {
                    int index = random.Next(allPrimes.Count);
                    if (!selectedIndices.Contains(index))
                    {
                        selectedIndices.Add(index);
                        selectedPrimes.Add(allPrimes[index]);
                    }
                }
                
                // Sort the primes for easier reading
                selectedPrimes.Sort();
                
                return Ok(new
                {
                    message = $"Đã tạo ngẫu nhiên {count} số nguyên tố nhỏ hơn hoặc bằng {max}",
                    primes = selectedPrimes,
                    totalPrimesAvailable = allPrimes.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Lỗi tạo số nguyên tố: {ex.Message}" });
            }
        }
    }
}