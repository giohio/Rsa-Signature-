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
using RsaSignApi.Models;
using RsaSignApi.Utils;
using RsaSignApi.Model.RequestModels;

namespace RsaSignApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SignController : ControllerBase
    {
        private readonly MongoDbContext _context;
        public SignController(MongoDbContext context) => _context = context;

        // 1. GenerateKeys (unchanged)...
        [HttpPost("generate-keys")]
        public async Task<IActionResult> GenerateKeys([FromBody] GenerateKeyModel model)
        {
            if (model.KeySize < 2048)
                return BadRequest("Key size must be at least 2048 bits");

            using var rsa = RSA.Create(model.KeySize);
            var pub = Convert.ToBase64String(rsa.ExportRSAPublicKey());
            var priv = Convert.ToBase64String(rsa.ExportRSAPrivateKey());

            var user = await _context.Users.Find(u => u.Id == model.UserId).FirstOrDefaultAsync();
            if (user == null) return NotFound("User not found");

            var signRecord = new Sign
            {
                UserId = model.UserId!,
                PublicKey = pub,
                PrivateKey = priv,
                Email = user.Email,
                FullName = user.FullName,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Signs.InsertOneAsync(signRecord);
            return Ok(new { message = "Keys generated", publicKey = pub, signId = signRecord.Id });
        }

        // 2. GetSignatures (unchanged)...
        [HttpGet("list/{userId}")]
        public async Task<IActionResult> GetSignatures(string userId)
        {
            var sigs = await _context.Signs
                .Find(s => s.UserId == userId)
                .Project(s => new { s.Id, s.PublicKey, s.CreatedAt, s.Email, s.FullName })
                .ToListAsync();
            if (!sigs.Any()) return NotFound("No signatures found");
            return Ok(new { message = "Retrieved", signatures = sigs });
        }

        // 3. DeleteSignature (unchanged)...
        [HttpDelete("delete/{userId}/{signId}")]
        public async Task<IActionResult> DeleteSignature(string userId, string signId)
        {
            var res = await _context.Signs.DeleteOneAsync(s => s.Id == signId && s.UserId == userId);
            if (res.DeletedCount == 0) return NotFound("Not found or not yours");
            return Ok(new { message = "Deleted" });
        }

        // 4. SignDocument: PDF trực tiếp, else convert→PDF rồi ký
        [HttpPost("sign-document")]
        public async Task<IActionResult> SignDocument([FromForm] SignDocumentModel model)
        {
            if (model.UserId == null || model.SignId == null || model.File == null)
                return BadRequest("UserId, SignId and File are required");

            // 4.1. Lấy record + user
            var rec = await _context.Signs
                .Find(s => s.Id == model.SignId && s.UserId == model.UserId)
                .FirstOrDefaultAsync();
            if (rec == null) return NotFound("Signature record not found");

            var user = await _context.Users.Find(u => u.Id == model.UserId).FirstOrDefaultAsync();
            if (user == null) return NotFound("User not found");

            // 4.2. Đọc toàn bộ file input
            byte[] inputBytes;
            using (var msIn = new MemoryStream())
            {
                await model.File.CopyToAsync(msIn);
                inputBytes = msIn.ToArray();
            }
            var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();

            // 4.3. Tạo certificate self-signed từ privateKey + user info
            X509Certificate2 cert;
            try
            {
                var privBytes = Convert.FromBase64String(rec.PrivateKey);
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(privBytes, out _);
                var pair = DotNetUtilities.GetRsaKeyPair(rsa);
                string subject = $"CN={user.FullName}, E={user.Email}";
                cert = CertificateHelper.CreateCertificateFromKeyPair(pair, subject);
            }
            catch (Exception ex)
            {
                return BadRequest($"Cannot create certificate: {ex.Message}");
            }

            // 4.4. Nếu đã là PDF, ký luôn; ngược lại convert → PDF rồi ký
            byte[] pdfToSign = ext == ".pdf"
                ? inputBytes
                : ConvertToPdf(inputBytes, model.File.FileName);

            byte[] signedPdf;
            try
            {
                signedPdf = SignPdfBytes(pdfToSign, cert);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error while signing PDF: {ex.Message}");
            }

            // 4.5. (không bắt buộc) lưu hash vào Mongo
            _ = Task.Run(async () =>
            {
                try
                {
                    var hash = Convert.ToBase64String(SHA256.HashData(signedPdf));
                    await _context.Signs.UpdateOneAsync(
                        s => s.Id == model.SignId,
                        Builders<Sign>.Update.Set(s => s.DocumentHash, hash)
                    );
                }
                catch { }
            });

            // 4.6. Trả file PDF đã ký
            var fileName = Path.GetFileNameWithoutExtension(model.File.FileName) + "-signed.pdf";
            return File(
                signedPdf,              // nội dung file
                "application/pdf",      // content type
                fileName                // download file name
            );
        }

        // ----- Helpers -----

        // Ký PDF bytes bằng iTextSharp (PAdES/CAdES)
        private byte[] SignPdfBytes(byte[] pdfBytes, X509Certificate2 cert)
        {
            using var reader = new PdfReader(pdfBytes);
            using var signedStream = new MemoryStream();
            var stamper = PdfStamper.CreateSignature(reader, signedStream, '\0');
            var appearance = stamper.SignatureAppearance;
            appearance.Reason = "Digitally signed by RsaSignApi";
            appearance.Location = "Vietnam";
            appearance.SetVisibleSignature(new Rectangle(36, 748, 144, 780), 1, "SignatureField");

            var bcCert = DotNetUtilities.FromX509Certificate(cert);
            var privateKey = DotNetUtilities.GetKeyPair(cert.GetRSAPrivateKey()).Private;
            var pks = new PrivateKeySignature(privateKey, DigestAlgorithms.SHA256);

            MakeSignature.SignDetached(
                appearance,
                pks,
                new[] { bcCert },
                null, null, null, 0,
                CryptoStandard.CADES
            );

            stamper.Close();
            reader.Close();
            return signedStream.ToArray();
        }

        // Convert bất kỳ file Office → PDF qua LibreOffice headless
        private byte[] ConvertToPdf(byte[] inputBytes, string originalFileName)
        {
            // 1. Ghi file tạm
            string tmpIn = Path.Combine(Path.GetTempPath(),
                                      $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}");
            System.IO.File.WriteAllBytes(tmpIn, inputBytes);

            // 2. Gọi soffice để convert
            var psi = new ProcessStartInfo
            {
                FileName = "soffice",
                Arguments = $"--headless --convert-to pdf --outdir \"{Path.GetTempPath()}\" \"{tmpIn}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            if (p == null || !p.WaitForExit(60_000) || p.ExitCode != 0)
                throw new InvalidOperationException("LibreOffice conversion failed.");

            // 3. Đọc PDF vừa tạo
            string tmpOut = Path.ChangeExtension(tmpIn, ".pdf");
            if (!System.IO.File.Exists(tmpOut))
                throw new FileNotFoundException("Converted PDF not found", tmpOut);
            var pdfBytes = System.IO.File.ReadAllBytes(tmpOut);

            // 4. Xóa file tạm
            try
            {
                System.IO.File.Delete(tmpIn);
                System.IO.File.Delete(tmpOut);
            }
            catch { }

            return pdfBytes;
        }

        // 5. VerifySignature (unchanged)...
        [HttpPost("verify-signature")]
        public async Task<IActionResult> VerifySignature([FromForm] VerifySignatureModel model)
        {
            if (model.File == null) return BadRequest("File is required");

            byte[] bytes;
            using var ms = new MemoryStream();
            await model.File.CopyToAsync(ms);
            bytes = ms.ToArray();

            try
            {
                using var reader = new PdfReader(bytes);
                var af = reader.AcroFields;
                var names = af.GetSignatureNames();
                if (!names.Any())
                    return Ok(new { isValid = false, message = "No signature found" });

                var pkcs7 = af.VerifySignature(names[0]);
                if (!pkcs7.Verify())
                    return Ok(new { isValid = false, message = "Invalid signature" });

                var dotCert = new X509Certificate2(
                    DotNetUtilities.ToX509Certificate(pkcs7.SigningCertificate));
                var subject = dotCert.Subject;
                string fullName = null, email = null;
                foreach (var p in subject.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = p.Split('=', 2);
                    if (kv[0].Trim().Equals("CN", StringComparison.OrdinalIgnoreCase))
                        fullName = kv[1];
                    if (kv[0].Trim().Equals("E", StringComparison.OrdinalIgnoreCase))
                        email = kv[1];
                }

                return Ok(new
                {
                    isValid = true,
                    message = "Signature valid",
                    fullName,
                    email
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error while verifying PDF: {ex.Message}");
            }
        }
    }
}
