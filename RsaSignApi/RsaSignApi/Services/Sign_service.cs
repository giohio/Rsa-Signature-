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
                return (false, "Độ dài khóa tối thiểu là 2048 bit", null, null, null);

            using var rsa = RSA.Create(model.KeySize);
            var pub = Convert.ToBase64String(rsa.ExportRSAPublicKey());
            var priv = Convert.ToBase64String(rsa.ExportRSAPrivateKey());

            var user = await _context.Users.Find(u => u.Id == model.UserId).FirstOrDefaultAsync();
            if (user == null) return (false, "Không tìm thấy người dùng", null, null, null);

            // Generate keys but DO NOT save them to database yet
            // Frontend will call save-key-pair endpoint later to save
            return (true, "Tạo khóa thành công", pub, priv, null);
        }

        public async Task<(bool Success, string Message, List<object> Signatures)> GetSignaturesAsync(string userId)
        {
            var sigs = await _context.Signs
                .Find(s => s.UserId == userId)
                .Project(s => new { s.Id, s.PublicKey, s.CreatedAt, s.Email, s.FullName, s.SignatureName, s.SignatureType, s.IsActive })
                .ToListAsync();
            if (!sigs.Any()) return (false, "Không tìm thấy chữ ký", null);
            return (true, "Đã truy xuất", sigs.Cast<object>().ToList());
        }

        public async Task<(bool Success, string Message)> DeleteSignatureAsync(string userId, string signId)
        {
            var res = await _context.Signs.DeleteOneAsync(s => s.Id == signId && s.UserId == userId);
            if (res.DeletedCount == 0) return (false, "Không tìm thấy hoặc không phải của bạn");
            return (true, "Đã xóa");
        }

        public async Task<(bool Success, string Message, byte[] SignedFile, string FileName)> SignDocumentAsync(SignDocumentModel model)
        {
            try 
            {
                // Validate required parameters
                if (string.IsNullOrWhiteSpace(model.UserId))
                {
                    _logger.LogError("SignDocumentAsync: UserId is null or empty");
                    return (false, "Mã người dùng là bắt buộc", null, null);
                }

                if (string.IsNullOrWhiteSpace(model.SignId))
                {
                    _logger.LogError("SignDocumentAsync: SignId is null or empty");
                    return (false, "Mã chữ ký là bắt buộc", null, null);
                }

                if (model.File == null || model.File.Length == 0)
                {
                    _logger.LogError("SignDocumentAsync: File is null or empty");
                    return (false, "Tệp là bắt buộc và không được để trống", null, null);
                }

                // Retrieve signature record
                var signRecord = await _context.Signs
                    .Find(s => s.Id == model.SignId && s.UserId == model.UserId)
                    .FirstOrDefaultAsync();

                if (signRecord == null)
                {
                    _logger.LogError($"SignDocumentAsync: No signature found. SignId: {model.SignId}, UserId: {model.UserId}");
                    return (false, "Không tìm thấy chữ ký", null, null);
                }

                // Validate private key
                if (string.IsNullOrEmpty(signRecord.PrivateKey))
                {
                    _logger.LogError($"SignDocumentAsync: No private key found for signature. SignId: {model.SignId}");
                    return (false, "Không tìm thấy khóa riêng cho chữ ký này", null, null);
                }
                
                // Retrieve user
                var user = await _context.Users
                    .Find(u => u.Id == model.UserId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogError($"SignDocumentAsync: User not found. UserId: {model.UserId}");
                    return (false, "Không tìm thấy người dùng", null, null);
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
                    return (false, "Nội dung tệp trống", null, null);
                }
                
                // File type validation
                var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
                var supportedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt" };
                
                if (!supportedExtensions.Contains(ext))
                {
                    _logger.LogError($"SignDocumentAsync: Unsupported file format: {ext}");
                    return (false, $"Định dạng tệp không được hỗ trợ: {ext}", null, null);
                }
                
                // File size validation
                if (fileBytes.Length > 10 * 1024 * 1024) // 10MB limit
                {
                    _logger.LogError($"SignDocumentAsync: File size exceeds limit. Current size: {fileBytes.Length}");
                    return (false, "Kích thước tệp vượt quá giới hạn 10MB", null, null);
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
                        return (false, $"Không thể tạo chứng chỉ: cần khóa RSA 2048 trở nên mới có thể tạo", null, null);
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
                        return (false, $"Không thể chuẩn bị tài liệu: {ex.Message}", null, null);
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
                        return (false, $"Không thể ký tài liệu: {ex.Message}", null, null);
                    }
                    
                    string newFilename = ext == ".pdf" 
                        ? $"signed_{model.File.FileName}" 
                        : $"signed_{Path.GetFileNameWithoutExtension(model.File.FileName)}.pdf";
                    
                    _logger.LogInformation($"SignDocumentAsync: Document signed successfully. Output filename: {newFilename}");
                    return (true, $"Ký tài liệu thành công: {newFilename}", signedPdf, newFilename);
                }
                else
                {
                    _logger.LogInformation("SignDocumentAsync: Using detached signature mode");
                    // Detached signature (.sig)
                    // Hash the file
                    byte[] hashBytes;
                    switch (model.HashAlgorithm?.ToUpper())
                    {
                        case "MD5":
                            using (var md5 = MD5.Create())
                            {
                                hashBytes = md5.ComputeHash(fileBytes);
                            }
                            break;
                        case "SHA1":
                            using (var sha1 = SHA1.Create())
                            {
                                hashBytes = sha1.ComputeHash(fileBytes);
                            }
                            break;
                        case "SHA512":
                            using (var sha512 = SHA512.Create())
                            {
                                hashBytes = sha512.ComputeHash(fileBytes);
                            }
                            break;
                        case "SHA256":
                        default:
                            using (var sha256 = SHA256.Create())
                            {
                                hashBytes = sha256.ComputeHash(fileBytes);
                            }
                            break;
                    }
                    
                    // Sign the hash
                    var privateKeyBytes = Convert.FromBase64String(signRecord.PrivateKey);
                    using var rsa = RSA.Create();
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                    
                    // Use the appropriate hash algorithm
                    HashAlgorithmName rsaHashAlgo;
                    switch (model.HashAlgorithm?.ToUpper())
                    {
                        case "MD5":
                            rsaHashAlgo = HashAlgorithmName.MD5;
                            break;
                        case "SHA1":
                            rsaHashAlgo = HashAlgorithmName.SHA1;
                            break;
                        case "SHA512":
                            rsaHashAlgo = HashAlgorithmName.SHA512;
                            break;
                        case "SHA256":
                        default:
                            rsaHashAlgo = HashAlgorithmName.SHA256;
                            break;
                    }
                    
                    var signatureBytes = rsa.SignHash(hashBytes, rsaHashAlgo, RSASignaturePadding.Pkcs1);
                    // Return signature as .sig file
                    string sigFileName = $"{Path.GetFileNameWithoutExtension(model.File.FileName)}.sig";
                    return (true, "Tạo chữ ký tách rời thành công", signatureBytes, sigFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SignDocumentAsync: Unexpected error. Exception: {ex.Message}");
                return (false, $"Lỗi không xác định: {ex.Message}", null, null);
            }
        }

        public async Task<(bool Success, string Message, bool IsValid, string FullName, string Email)> VerifySignatureAsync(IFormFile file, IFormFile? originalFile = null, bool isEmbedded = true)
        {
            if (file == null) return (false, "Tệp chữ ký là bắt buộc", false, null, null);

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
                        return (true, "Không tìm thấy chữ ký trong PDF", false, null, null);

                    _logger.LogInformation($"Found {names.Count} signature(s) in PDF");
                    var pkcs7 = af.VerifySignature(names[0]);
                    
                    if (!pkcs7.Verify())
                        return (true, "Chữ ký PDF không hợp lệ", false, null, null);

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
                    return (true, "PDF signature is valid", true, fullName ?? string.Empty, email ?? string.Empty);
                }
                else
                {
                    // Verify detached signature (.sig file)
                    if (originalFile == null)
                        return (false, "Original file is required for detached signature verification", false, string.Empty, string.Empty);
                    
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
                    return (false, "Detached signature verification not implemented yet", false, string.Empty, string.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error verifying signature: {ex.Message}");
                if (ex.InnerException != null)
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                return (false, $"Lỗi không xác định: {ex.Message}", false, string.Empty, string.Empty);
            }
        }

        // Helper: Ký PDF bytes bằng iTextSharp
        private byte[] SignPdfBytes(byte[] pdfBytes, X509Certificate2 cert, string hashAlgorithm = "SHA256")
        {
            using var signedStream = new MemoryStream();
            try
            {
                _logger.LogInformation($"Starting SignPdfBytes with algorithm {hashAlgorithm}, PDF size: {pdfBytes.Length} bytes");
                var reader = new PdfReader(pdfBytes);
                var stamper = PdfStamper.CreateSignature(reader, signedStream, '\0');
                
                // Create the appearance
                var appearance = stamper.SignatureAppearance;
                appearance.Reason = "Digital Signature";
                appearance.Location = "Vietnam";
                appearance.SetVisibleSignature(new iTextSharp.text.Rectangle(100, 100, 300, 200), 1, "sig");
                
                var bcCert = DotNetUtilities.FromX509Certificate(cert);
                var privateKey = DotNetUtilities.GetKeyPair(cert.GetRSAPrivateKey()).Private;
                _logger.LogInformation("Successfully extracted private key from certificate");
                
                // Use the specified hash algorithm
                string digestAlgorithm;
                switch (hashAlgorithm?.ToUpperInvariant())
                {
                    case "MD5":
                        // MD5 is not available in DigestAlgorithms, use DigestAlgorithms.SHA1 as fallback
                        digestAlgorithm = DigestAlgorithms.SHA1;
                        _logger.LogWarning("MD5 is not available in DigestAlgorithms, using SHA1 instead");
                        break;
                    case "SHA1":
                        digestAlgorithm = DigestAlgorithms.SHA1;
                        break;
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
                throw new Exception($"Lỗi không xác định: {ex.Message}", ex);
            }
        }

        // Helper: Convert Office file → PDF qua LibreOffice headless hoặc xử lý text trực tiếp
        private byte[] ConvertToPdf(byte[] inputBytes, string originalFileName)
        {
            try
            {
                // 1. Ghi file tạm
                string tempDir = Environment.GetEnvironmentVariable("TMPDIR") ?? Path.GetTempPath();
                tempDir = string.IsNullOrEmpty(tempDir) ? "/tmp/libreoffice-conversion" : tempDir;
                
                // Ensure temp directory exists
                if (!Directory.Exists(tempDir))
                {
                    _logger.LogInformation($"ConvertToPdf: Creating temp directory at {tempDir}");
                    Directory.CreateDirectory(tempDir);
                }
                
                string fileName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
                string tmpIn = Path.Combine(tempDir, fileName);
                
                _logger.LogInformation($"ConvertToPdf: Creating temp file at {tmpIn}");
                System.IO.File.WriteAllBytes(tmpIn, inputBytes);

                // Make sure the file exists
                if (!System.IO.File.Exists(tmpIn))
                {
                    _logger.LogError($"ConvertToPdf: Failed to create temporary input file at {tmpIn}");
                    throw new FileNotFoundException("Could not create temporary input file", tmpIn);
                }

                // 2. Gọi soffice để convert
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "soffice",
                    Arguments = $"--headless --convert-to pdf --outdir \"{tempDir}\" \"{tmpIn}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = tempDir
                };
                
                _logger.LogInformation($"ConvertToPdf: Executing LibreOffice command: {psi.FileName} {psi.Arguments}");
                
                string output = string.Empty;
                string error = string.Empty;
                
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null)
                    {
                        _logger.LogError("ConvertToPdf: Failed to start LibreOffice process");
                        throw new InvalidOperationException("Failed to start LibreOffice process");
                    }
                    
                    output = p.StandardOutput.ReadToEnd();
                    error = p.StandardError.ReadToEnd();
                    
                    if (!p.WaitForExit(60_000) || p.ExitCode != 0)
                    {
                        _logger.LogError($"ConvertToPdf: LibreOffice process failed. Exit code: {p.ExitCode}, Error: {error}, Output: {output}");
                        throw new InvalidOperationException($"LibreOffice conversion failed. Exit code: {p.ExitCode}, Error: {error}");
                    }
                    
                    _logger.LogInformation($"ConvertToPdf: LibreOffice process completed successfully. Output: {output}");
                }

                // 3. Đọc PDF vừa tạo
                string tmpOut = Path.ChangeExtension(tmpIn, ".pdf");
                _logger.LogInformation($"ConvertToPdf: Looking for output PDF at {tmpOut}");
                
                if (!System.IO.File.Exists(tmpOut))
                {
                    _logger.LogError($"ConvertToPdf: Output PDF not found at {tmpOut}");
                    
                    // Try to find the output file with a different name pattern
                    string baseFileName = Path.GetFileNameWithoutExtension(originalFileName);
                    string[] possibleFiles = Directory.GetFiles(tempDir, $"{baseFileName}*.pdf");
                    
                    if (possibleFiles.Length > 0)
                    {
                        tmpOut = possibleFiles[0];
                        _logger.LogInformation($"ConvertToPdf: Found alternative output PDF at {tmpOut}");
                    }
                    else
                    {
                        // List files in temp directory to help debug
                        var files = Directory.GetFiles(tempDir);
                        _logger.LogInformation($"ConvertToPdf: Files in temp directory: {string.Join(", ", files)}");
                        
                        throw new FileNotFoundException($"Converted PDF not found. Output: {output}, Error: {error}", tmpOut);
                    }
                }
                
                var pdfBytes = System.IO.File.ReadAllBytes(tmpOut);
                _logger.LogInformation($"ConvertToPdf: Successfully read PDF, size: {pdfBytes.Length} bytes");

                // 4. Xóa file tạm
                try
                {
                    System.IO.File.Delete(tmpIn);
                    System.IO.File.Delete(tmpOut);
                    _logger.LogInformation("ConvertToPdf: Temporary files deleted successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"ConvertToPdf: Failed to delete temporary files: {ex.Message}");
                    // Continue execution even if temp file deletion fails
                }

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ConvertToPdf: Conversion failed: {ex.Message}");
                if (ex.InnerException != null)
                    _logger.LogError($"ConvertToPdf: Inner exception: {ex.InnerException.Message}");
                throw new Exception($"Không thể chuyển đổi tài liệu sang PDF: {ex.Message}", ex);
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
