using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Security;
using BCX509 = Org.BouncyCastle.X509; // Alias for BouncyCastle X509
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using RsaSignApi.Data;
using RsaSignApi.Models;
using RsaSignApi.Services;
using RsaSignApi.Utils;
using RsaSignApi.Model.RequestModels;

namespace RsaSignApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SignController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public SignController(MongoDbContext context)
        {
            _context = context;
        }

        // 1. GenerateKeys (unchanged)
        [HttpPost("generate-keys")]
        public async Task<IActionResult> GenerateKeys([FromBody] GenerateKeyModel model)
        {
            if (model.KeySize < 2048)
                return BadRequest("Key size must be at least 2048 bits");

            using var rsa = RSA.Create(model.KeySize);
            var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
            var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());

            var user = await _context.Users.Find(u => u.Id == model.UserId).FirstOrDefaultAsync();
            if (user == null)
                return NotFound("User not found");

            var signRecord = new Sign
            {
                UserId = model.UserId!,
                PublicKey = publicKey,
                PrivateKey = privateKey,
                Email = user.Email,
                FullName = user.FullName,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Signs.InsertOneAsync(signRecord);
            return Ok(new { message = "Keys generated successfully", publicKey, signId = signRecord.Id });
        }

        // 2. GetSignatures (unchanged)
        [HttpGet("list/{userId}")]
        public async Task<IActionResult> GetSignatures(string userId)
        {
            var signatures = await _context.Signs
                .Find(s => s.UserId == userId)
                .Project(s => new { s.Id, s.PublicKey, s.CreatedAt, s.Email, s.FullName })
                .ToListAsync();

            if (!signatures.Any())
                return NotFound("No signatures found for this user");

            return Ok(new { message = "Signatures retrieved successfully", signatures });
        }

        // 3. DeleteSignature (unchanged)
        [HttpDelete("delete/{userId}/{signId}")]
        public async Task<IActionResult> DeleteSignature(string userId, string signId)
        {
            var result = await _context.Signs.DeleteOneAsync(s => s.Id == signId && s.UserId == userId);
            if (result.DeletedCount == 0)
                return NotFound("Signature not found or does not belong to this user");

            return Ok(new { message = "Signature deleted successfully" });
        }

        // 4. SignDocument: supports PDF (PAdES) & DOCX/XLSX/PPTX and more via GroupDocs
        [HttpPost("sign-document")]
        public async Task<IActionResult> SignDocument([FromForm] SignDocumentModel model)
        {
            if (model.UserId == null || model.SignId == null || model.File == null)
                return BadRequest("UserId, SignId, and File are required");

            // Fetch record & user
            var signRecord = await _context.Signs.Find(s => s.Id == model.SignId && s.UserId == model.UserId).FirstOrDefaultAsync();
            if (signRecord == null) return NotFound("Sign not found or does not belong to this user");
            var user = await _context.Users.Find(u => u.Id == model.UserId).FirstOrDefaultAsync();
            if (user == null) return NotFound("User not found");

            // Read file bytes
            byte[] inputBytes;
            using (var ms = new MemoryStream()) { await model.File.CopyToAsync(ms); inputBytes = ms.ToArray(); }
            var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();

            // Create self-signed certificate
            X509Certificate2 cert;
            try
            {
                var privateKeyBytes = Convert.FromBase64String(signRecord.PrivateKey);
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                var keyPair = DotNetUtilities.GetRsaKeyPair(rsa);
                string subject = $"CN={user.FullName}, E={user.Email}";
                cert = CertificateHelper.CreateCertificateFromKeyPair(keyPair, subject);
            }
            catch (Exception ex)
            {
                return BadRequest($"Cannot create certificate: {ex.Message}");
            }

            byte[] signedBytes;
            string outExt = ext;

            if (ext == ".pdf")
            {
                // PDF signing via iTextSharp (PAdES/CAdES)
                try
                {
                    using var reader = new PdfReader(inputBytes);
                    using var signedStream = new MemoryStream();
                    var stamper = PdfStamper.CreateSignature(reader, signedStream, '\0');
                    var appearance = stamper.SignatureAppearance;
                    appearance.Reason = "Document digitally signed by RsaSignApi";
                    appearance.Location = "Created in Vietnam";
                    appearance.SetVisibleSignature(new Rectangle(36, 748, 144, 780), 1, "SignatureField");
                    var bcCert = DotNetUtilities.FromX509Certificate(cert);
                    var privateKeyBC = DotNetUtilities.GetKeyPair(cert.GetRSAPrivateKey()).Private;
                    var chain = new[] { bcCert };
                    var pks = new PrivateKeySignature(privateKeyBC, DigestAlgorithms.SHA256);
                    MakeSignature.SignDetached(appearance, pks, chain, null, null, null, 0, CryptoStandard.CADES);
                    stamper.Close(); reader.Close();
                    signedBytes = signedStream.ToArray();
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error while signing PDF: {ex.Message}");
                }
            }
            else
            {
                // Other formats via GroupDocs
                try
                {
                    var signService = new DichVuKyTapTin(cert);
                    var result = await signService.KyTheoLoaiAsync(new MemoryStream(inputBytes), model.File.FileName);
                    signedBytes = result.Data;
                    outExt = result.Extension;
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error while signing document: {ex.Message}");
                }
            }

            // Optional: update document hash
            _ = Task.Run(async () => {
                try
                {
                    var hash = Convert.ToBase64String(SHA256.HashData(signedBytes));
                    var update = Builders<Sign>.Update.Set(s => s.DocumentHash, hash);
                    await _context.Signs.UpdateOneAsync(s => s.Id == model.SignId, update);
                }
                catch { /* Ignored */ }
            });

            var fileName = Path.GetFileNameWithoutExtension(model.File.FileName) + $"-signed{outExt}";
            return File(signedBytes, GetContentType(outExt), fileName);
        }

        // 5. VerifySignature: supports PDF & other formats
        [HttpPost("verify-signature")]
        public async Task<IActionResult> VerifySignature([FromForm] VerifySignatureModel model)
        {
            if (model.File == null) return BadRequest("File (signed) is required");
            byte[] fileBytes;
            using (var ms = new MemoryStream()) { await model.File.CopyToAsync(ms); fileBytes = ms.ToArray(); }
            var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();

            if (ext == ".pdf")
            {
                // PDF verification via iTextSharp
                try
                {
                    using var reader = new PdfReader(fileBytes);
                    var af = reader.AcroFields;
                    var names = af.GetSignatureNames();
                    if (!names.Any())
                        return Ok(new { isValid = false, message = "No signature field found in PDF" });

                    var pkcs7 = af.VerifySignature(names[0]);
                    if (!pkcs7.Verify())
                        return Ok(new { isValid = false, message = "Signature is cryptographically invalid" });

                    var bcCert = pkcs7.SigningCertificate;
                    var dotnetCert = new X509Certificate2(DotNetUtilities.ToX509Certificate(bcCert));
                    var subject = dotnetCert.Subject;
                    string fullName = null, email = null;
                    foreach (var part in subject.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = part.Trim().Split('=', 2);
                        if (kv.Length == 2)
                        {
                            if (kv[0].Equals("CN", StringComparison.OrdinalIgnoreCase)) fullName = kv[1];
                            if (kv[0].Equals("E", StringComparison.OrdinalIgnoreCase)) email = kv[1];
                        }
                    }
                    return Ok(new { isValid = true, message = "Signature is valid", fullName, email });
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error while verifying PDF: {ex.Message}");
                }
            }
            else
            {
                // Other formats via GroupDocs
                try
                {
                    var verifier = new DichVuXacMinhChuKy();
                    var result = await verifier.XacMinhTheoLoaiAsync(new MemoryStream(fileBytes), model.File.FileName);
                    return Ok(new
                    {
                        isValid = result.HopLe,
                        message = result.ThanhCong ? (result.HopLe ? "Signature is valid" : "Signature is invalid") : result.ThongBaoLoi,
                        fullName = result.TenNguoiKy,
                        email = result.EmailNguoiKy,
                        signedAt = result.ThoiGianKy
                    });
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error while verifying document: {ex.Message}");
                }
            }
        }

        private string GetContentType(string ext) => ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".p7s" => "application/pkcs7-signature",
            _ => "application/octet-stream"
        };
    }
}
