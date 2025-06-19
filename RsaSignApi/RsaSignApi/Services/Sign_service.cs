using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using RsaSignApi.Data;
using RsaSignApi.Model;
using RsaSignApi.Utils;
using RsaSignApi.Model.RequestModels;
using Microsoft.Extensions.Logging;

namespace RsaSignApi.Services
{
    public interface ISignService
    {
        Task<(bool Success, string Message, string PublicKey, string PrivateKey, string SignId)> GenerateKeysAsync(GenerateKeyModel model);
        Task<(bool Success, string Message, List<object> Signatures)> GetSignaturesAsync(string userId);
        Task<(bool Success, string Message)> DeleteSignatureAsync(string userId, string signId);
        Task<(bool Success, string Message, byte[] SignedFile, string FileName)> SignDocumentAsync(SignDocumentModel model);
        Task<(bool Success, string Message, bool IsValid, string FullName, string Email)> VerifySignatureAsync(IFormFile file, IFormFile? originalFile = null, bool isEmbedded = true);
    }

    public class SignService : ISignService
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<SignService> _logger;

        public SignService(MongoDbContext context, ILogger<SignService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Success, string Message, string PublicKey, string PrivateKey, string SignId)> GenerateKeysAsync(GenerateKeyModel model)
        {
            if (model.KeySize < 2048)
                return (false, "Key size must be at least 2048 bits", null, null, null);

            using var rsa = RSA.Create(model.KeySize);
            var pub = Convert.ToBase64String(rsa.ExportRSAPublicKey());
            var priv = Convert.ToBase64String(rsa.ExportRSAPrivateKey());

            var user = await _context.Users.Find(u => u.Id == model.UserId).FirstOrDefaultAsync();
            if (user == null) return (false, "User not found", null, null, null);

            // Generate keys but DO NOT save them to database yet
            // Frontend will call save-key-pair endpoint later to save
            return (true, "Keys generated", pub, priv, null);
        }

        public async Task<(bool Success, string Message, List<object> Signatures)> GetSignaturesAsync(string userId)
        {
            var sigs = await _context.Signs
                .Find(s => s.UserId == userId)
                .Project(s => new { s.Id, s.PublicKey, s.CreatedAt, s.Email, s.FullName, s.SignatureName, s.SignatureType, s.IsActive })
                .ToListAsync();
            if (!sigs.Any()) return (false, "No signatures found", null);
            return (true, "Retrieved", sigs.Cast<object>().ToList());
        }

        public async Task<(bool Success, string Message)> DeleteSignatureAsync(string userId, string signId)
        {
            var res = await _context.Signs.DeleteOneAsync(s => s.Id == signId && s.UserId == userId);
            if (res.DeletedCount == 0) return (false, "Not found or not yours");
            return (true, "Deleted");
        }

        public async Task<(bool Success, string Message, byte[] SignedFile, string FileName)> SignDocumentAsync(SignDocumentModel model)
        {
            try 
            {
                // Validate required parameters
                if (string.IsNullOrWhiteSpace(model.UserId))
                {
                    _logger.LogError("SignDocumentAsync: UserId is null or empty");
                    return (false, "User ID is required", null, null);
                }

                if (string.IsNullOrWhiteSpace(model.SignId))
                {
                    _logger.LogError("SignDocumentAsync: SignId is null or empty");
                    return (false, "Signature ID is required", null, null);
                }

                if (model.File == null || model.File.Length == 0)
                {
                    _logger.LogError("SignDocumentAsync: File is null or empty");
                    return (false, "File is required and cannot be empty", null, null);
                }

                // Retrieve signature record
                var signRecord = await _context.Signs
                    .Find(s => s.Id == model.SignId && s.UserId == model.UserId)
                    .FirstOrDefaultAsync();

                if (signRecord == null)
                {
                    _logger.LogError($"SignDocumentAsync: No signature found. SignId: {model.SignId}, UserId: {model.UserId}");
                    return (false, "Signature not found", null, null);
                }

                // Validate private key
                if (string.IsNullOrEmpty(signRecord.PrivateKey))
                {
                    _logger.LogError($"SignDocumentAsync: No private key found for signature. SignId: {model.SignId}");
                    return (false, "No private key found for this signature", null, null);
                }
                
                // Retrieve user
                var user = await _context.Users
                    .Find(u => u.Id == model.UserId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogError($"SignDocumentAsync: User not found. UserId: {model.UserId}");
                    return (false, "User not found", null, null);
                }

                // Read file content
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await model.File.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }
                
                // Validate file content
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _logger.LogError("SignDocumentAsync: File content is empty after reading");
                    return (false, "File content is empty", null, null);
                }
                
                // File type validation
                var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
                var supportedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt" };
                
                if (!supportedExtensions.Contains(ext))
                {
                    _logger.LogError($"SignDocumentAsync: Unsupported file format: {ext}");
                    return (false, $"Unsupported file format: {ext}", null, null);
                }
                
                // File size validation
                if (fileBytes.Length > 10 * 1024 * 1024) // 10MB limit
                {
                    _logger.LogError($"SignDocumentAsync: File size exceeds limit. Current size: {fileBytes.Length}");
                    return (false, "File size exceeds 10MB limit", null, null);
                }

                // Branch for embedded or detached signature
                if (model.UseEmbeddedSign)
                {
                    _logger.LogInformation("SignDocumentAsync: Using embedded signature mode");
                    
                    // Create certificate from private key
                    X509Certificate2 cert;
                    try
                    {
                        var privateKeyBytes = Convert.FromBase64String(signRecord.PrivateKey);
                        using var rsa = RSA.Create();
                        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                        var keyPair = DotNetUtilities.GetRsaKeyPair(rsa);
                        string subject = $"CN={user.FullName}, E={user.Email}";
                        cert = CertificateHelper.CreateCertificateFromKeyPair(keyPair, subject);
                        _logger.LogInformation("SignDocumentAsync: Certificate created successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"SignDocumentAsync: Failed to create certificate: {ex.Message}");
                        return (false, $"Failed to create certificate: {ex.Message}", null, null);
                    }
                    
                    // Process file based on extension
                    _logger.LogInformation($"SignDocumentAsync: Processing file '{model.File.FileName}' with extension {ext}");
                    byte[] pdfToSign;
                    try
                    {
                        if (ext == ".pdf") 
                        {
                            pdfToSign = fileBytes;
                            _logger.LogInformation("SignDocumentAsync: Using direct PDF file");
                        }
                        else 
                        {
                            _logger.LogInformation("SignDocumentAsync: Converting file to PDF");
                            pdfToSign = ConvertToPdf(fileBytes, model.File.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"SignDocumentAsync: Failed to convert to PDF: {ex.Message}");
                        return (false, $"Failed to prepare document: {ex.Message}", null, null);
                    }
                    
                    // Sign the PDF
                    byte[] signedPdf;
                    try
                    {
                        signedPdf = SignPdfBytes(pdfToSign, cert, model.HashAlgorithm);
                        _logger.LogInformation($"SignDocumentAsync: PDF signed successfully, size: {signedPdf.Length} bytes");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"SignDocumentAsync: Failed to sign PDF: {ex.Message}");
                        return (false, $"Failed to sign document: {ex.Message}", null, null);
                    }
                    
                    string newFilename = ext == ".pdf" 
                        ? $"signed_{model.File.FileName}" 
                        : $"signed_{Path.GetFileNameWithoutExtension(model.File.FileName)}.pdf";
                    
                    _logger.LogInformation($"SignDocumentAsync: Document signed successfully. Output filename: {newFilename}");
                    return (true, "Document signed successfully", signedPdf, newFilename);
                }
                else
                {
                    _logger.LogInformation("SignDocumentAsync: Using detached signature mode");
                    // Detached signature (.sig)
                    // Hash the file
                    byte[] hashBytes;
                    if (model.HashAlgorithm?.ToUpper() == "SHA512")
                    {
                        using var sha512 = SHA512.Create();
                        hashBytes = sha512.ComputeHash(fileBytes);
                    }
                    else
                    {
                        using var sha256 = SHA256.Create();
                        hashBytes = sha256.ComputeHash(fileBytes);
                    }
                    // Sign the hash
                    var privateKeyBytes = Convert.FromBase64String(signRecord.PrivateKey);
                    using var rsa = RSA.Create();
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                    var rsaHashAlgo = model.HashAlgorithm?.ToUpper() == "SHA512" 
                        ? HashAlgorithmName.SHA512 
                        : HashAlgorithmName.SHA256;
                    var signatureBytes = rsa.SignHash(hashBytes, rsaHashAlgo, RSASignaturePadding.Pkcs1);
                    // Return signature as .sig file
                    string sigFileName = $"{Path.GetFileNameWithoutExtension(model.File.FileName)}.sig";
                    return (true, "Detached signature generated successfully", signatureBytes, sigFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SignDocumentAsync: Unexpected error. Exception: {ex.Message}");
                return (false, $"Unexpected error: {ex.Message}", null, null);
            }
        }

        public async Task<(bool Success, string Message, bool IsValid, string FullName, string Email)> VerifySignatureAsync(IFormFile file, IFormFile? originalFile = null, bool isEmbedded = true)
        {
            if (file == null) return (false, "Signature file is required", false, null, null);

            try
            {
                if (isEmbedded)
                {
                    _logger.LogInformation("Verifying embedded PDF signature");
                    byte[] pdfBytes;
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        pdfBytes = ms.ToArray();
                    }

                    using var reader = new PdfReader(pdfBytes);
                    var af = reader.AcroFields;
                    var names = af.GetSignatureNames();
                    
                    if (!names.Any())
                        return (true, "No signature found in PDF", false, null, null);

                    _logger.LogInformation($"Found {names.Count} signature(s) in PDF");
                    var pkcs7 = af.VerifySignature(names[0]);
                    
                    if (!pkcs7.Verify())
                        return (true, "PDF signature is invalid", false, null, null);

                    var dotCert = new X509Certificate2(
                        DotNetUtilities.ToX509Certificate(pkcs7.SigningCertificate));
                    var subject = dotCert.Subject;
                    _logger.LogInformation($"Certificate Subject: {subject}");

                    string fullName = null, email = null;
                    // Parse Subject using X500DistinguishedName
                    var dn = new X500DistinguishedName(subject);
                    var attributes = dn.Name.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(attr => attr.Trim().Split('=', 2))
                        .Where(kv => kv.Length == 2)
                        .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim());

                    if (attributes.TryGetValue("CN", out var cnValue))
                        fullName = cnValue;
                    if (attributes.TryGetValue("E", out var eValue))
                        email = eValue;

                    _logger.LogInformation($"Parsed FullName: {fullName}, Email: {email}");
                    return (true, "PDF signature is valid", true, fullName, email);
                }
                else
                {
                    // Verify detached signature (.sig file)
                    if (originalFile == null)
                        return (false, "Original file is required for detached signature verification", false, null, null);
                    
                    _logger.LogInformation("Verifying detached signature");
                    
                    // Read signature bytes
                    byte[] signatureBytes;
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        signatureBytes = ms.ToArray();
                    }
                    
                    // Read original file bytes
                    byte[] originalBytes;
                    using (var ms = new MemoryStream())
                    {
                        await originalFile.CopyToAsync(ms);
                        originalBytes = ms.ToArray();
                    }
                    
                    // TODO: Implement detached signature verification
                    // This is just a placeholder for now
                    return (false, "Detached signature verification not implemented yet", false, null, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error verifying signature: {ex.Message}");
                if (ex.InnerException != null)
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                return (false, $"Error verifying signature: {ex.Message}", false, null, null);
            }
        }

        // Helper: Ký PDF bytes bằng iTextSharp
        private byte[] SignPdfBytes(byte[] pdfBytes, X509Certificate2 cert, string hashAlgorithm = "SHA256")
        {
            try
            {
                _logger.LogInformation($"Starting SignPdfBytes with algorithm {hashAlgorithm}, PDF size: {pdfBytes.Length} bytes");
                
                using var reader = new PdfReader(pdfBytes);
                _logger.LogInformation($"PDF opened successfully, pages: {reader.NumberOfPages}");
                
                using var signedStream = new MemoryStream();
                var stamper = PdfStamper.CreateSignature(reader, signedStream, '\0');
                _logger.LogInformation("PDF stamper created successfully");
                
                var appearance = stamper.SignatureAppearance;
                appearance.Reason = "Digitally signed by RsaSignApi";
                appearance.Location = "Vietnam";
                appearance.SetVisibleSignature(new Rectangle(36, 748, 144, 780), 1, "SignatureField");
                _logger.LogInformation("Signature appearance set successfully");

                var bcCert = DotNetUtilities.FromX509Certificate(cert);
                var privateKey = DotNetUtilities.GetKeyPair(cert.GetRSAPrivateKey()).Private;
                _logger.LogInformation("Successfully extracted private key from certificate");
                
                // Use the specified hash algorithm
                string digestAlgorithm;
                switch (hashAlgorithm?.ToUpperInvariant())
                {
                    case "SHA512":
                        digestAlgorithm = DigestAlgorithms.SHA512;
                        break;
                    case "SHA384":
                        digestAlgorithm = DigestAlgorithms.SHA384;
                        break;
                    case "SHA256":
                    default:
                        digestAlgorithm = DigestAlgorithms.SHA256;
                        break;
                }
                _logger.LogInformation($"Using digest algorithm: {digestAlgorithm}");

                // Create the signature
                var pks = new PrivateKeySignature(privateKey, digestAlgorithm);
                _logger.LogInformation("External signature object created");
                
                // Sign with CADES standard instead of CMS
                _logger.LogInformation("Starting MakeSignature.SignDetached with CADES standard");
                MakeSignature.SignDetached(
                    appearance,
                    pks,
                    new[] { bcCert },
                    null, 
                    null, 
                    null, 
                    0,
                    CryptoStandard.CADES
                );
                _logger.LogInformation("MakeSignature.SignDetached completed successfully");
                
                var result = signedStream.ToArray();
                _logger.LogInformation($"SignPdfBytes completed successfully, result size: {result.Length} bytes");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SignPdfBytes: {ex.Message}");
                if (ex.InnerException != null)
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw new Exception($"Error in SignPdfBytes: {ex.Message}", ex);
            }
        }

        // Helper: Convert Office file → PDF qua LibreOffice headless hoặc xử lý text trực tiếp
        private byte[] ConvertToPdf(byte[] inputBytes, string originalFileName)
        {
            try
            {
                // 1. Ghi file tạm
                string tmpIn = Path.Combine(Path.GetTempPath(),
                                          $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}");
                System.IO.File.WriteAllBytes(tmpIn, inputBytes);

                // 2. Gọi soffice để convert
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "soffice",
                    Arguments = $"--headless --convert-to pdf --outdir \"{Path.GetTempPath()}\" \"{tmpIn}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = System.Diagnostics.Process.Start(psi);
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
            catch (Exception ex)
            {
                throw new Exception($"Error converting to PDF: {ex.Message}", ex);
            }
        }

        private byte[] ToBigEndian(System.Numerics.BigInteger v)
        {
            var bs = v.ToByteArray();
            if (bs.Length > 0 && bs[^1] == 0) Array.Resize(ref bs, bs.Length - 1);
            Array.Reverse(bs);
            return bs;
        }
        
        private System.Numerics.BigInteger ModInverse(System.Numerics.BigInteger a, System.Numerics.BigInteger m)
        {
            System.Numerics.BigInteger m0 = m, x0 = 0, x1 = 1;
            while (a > 1)
            {
                var q0 = a / m;
                var t = m;
                m = a % m;
                a = t;
                t = x0;
                x0 = x1 - q0 * x0;
                x1 = t;
            }
            if (x1 < 0) x1 += m0;
            return x1;
        }
    }
}
