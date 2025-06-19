using System.Threading.Tasks;
using System.Numerics;
using System;
using System.Security.Cryptography;
using System.IO;
using RsaSignApi.Data;
using RsaSignApi.Model;
using RsaSignApi.Model.RequestModels;
using System.Collections.Generic;
using System.Text.Json;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RsaSignApi.Services
{
    public interface IManualSignService
    {
        /// <summary>
        /// Validate RSA parameters p, q, e, d.
        /// </summary>
        Task<(bool Success, string Message)> ValidateParamsAsync(ManualSignParamsModel model);
        
        /// <summary>
        /// Generate key pair from p, q, e, d parameters.
        /// </summary>
        Task<(bool Success, string Message, string PublicKey, string PrivateKey, string SignId)> GenerateKeyPairFromParamsAsync(ManualSignParamsModel model);
        
        /// <summary>
        /// Sign data using p, q, e, d parameters.
        /// </summary>
        Task<(bool Success, string Message, string Signature)> SignDataFromParamsAsync(ManualSignParamsModel model);
        
        /// <summary>
        /// Automatically generate keys and sign data.
        /// </summary>
        Task<(bool Success, string Message, string PublicKey, string PrivateKey, string Signature)> AutoSignDataAsync(ManualSignAutoModel model);
        
        /// <summary>
        /// Save key pair to database.
        /// </summary>
        Task<(bool Success, string Message, string SignId)> SaveKeyPairAsync(SaveKeyPairModel model);
        
        /// <summary>
        /// Generate e, d values from p, q.
        /// </summary>
        Task<(bool Success, string Message, string E, string D)> GenerateEDFromPQAsync(GenerateEDFromPQModel model);
        
        /// <summary>
        /// Get key details for a specific user and signature ID.
        /// </summary>
        Task<(bool Success, string Message, Sign Signature)> GetKeyDetailsAsync(string userId, string signId);
        
        /// <summary>
        /// Update an existing key pair.
        /// </summary>
        Task<(bool Success, string Message, string SignId)> UpdateKeyPairAsync(UpdateKeyPairModel model);

        /// <summary>
        /// Generate RSA parameters p, q, e, d.
        /// </summary>
        Task<(bool Success, string Message, string P, string Q, string E, string D)> GenerateParamsAsync(int keySize);

        /// <summary>
        /// Import keys from files.
        /// </summary>
        Task<(bool Success, string Message, string PublicKey, string PrivateKey)> ImportKeysFromFilesAsync(ManualImportKeysModel model);

        /// <summary>
        /// Sign data manually using only n, e, d parameters without using RSA class
        /// </summary>
        Task<(bool Success, string Message, string Signature)> SignWithNEDAsync(string n, string e, string d, string data, string hashAlgorithm = "SHA256");
        
        /// <summary>
        /// Sign a file using RSA signature and return the signature
        /// </summary>
        Task<(bool Success, string Message, string Signature, string FileName)> SignFileAsync(IFormFile file, string userId, string signatureId, string hashAlgorithm = "SHA256", string privateKey = "", string n = "", string e = "", string d = "");
        
        /// <summary>
        /// Verify a file signature
        /// </summary>
        Task<(bool Success, string Message)> VerifyFileSignatureAsync(IFormFile file, string signature, string publicKey, string hashAlgorithm = "SHA256");
    }

    public class ManualSignService : IManualSignService
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<ManualSignService> _logger;
        private readonly TextWriter _errorOutput;

        public ManualSignService(MongoDbContext context, ILogger<ManualSignService> logger)
        {
            _context = context;
            _logger = logger;
            _errorOutput = Console.Error; // Direct terminal error output
        }

        public async Task<(bool Success, string Message)> ValidateParamsAsync(ManualSignParamsModel model)
        {
            var rsaResult = CreateRsaFromParams(model.P, model.Q, model.E, model.D);
            if (!rsaResult.Success)
                return (false, rsaResult.Message);
            
            rsaResult.Rsa.Dispose();
            return (true, "Tham số hợp lệ");
        }

        public async Task<(bool Success, string Message, string PublicKey, string PrivateKey, string SignId)> GenerateKeyPairFromParamsAsync(ManualSignParamsModel model)
        {
            try
            {
                // Parse the parameters
                BigInteger p, q, e, d, n;
                
                try
                {
                    p = BigInteger.Parse(model.P);
                    q = BigInteger.Parse(model.Q);
                    e = BigInteger.Parse(model.E);
                    d = BigInteger.Parse(model.D);
                    n = p * q;
                }
                catch (Exception ex)
                {
                    return (false, $"Lỗi phân tích tham số: {ex.Message}", null!, null!, null!);
                }
                
                // Validate parameters
                if (p < 2 || q < 2)
                    return (false, "Tham số p và q phải >= 2", null!, null!, null!);
                    
                var phi = (p - 1) * (q - 1);
                
                if (BigInteger.GreatestCommonDivisor(e, phi) != 1)
                    return (false, "e và phi(n) không nguyên tố cùng nhau", null!, null!, null!);
                    
                if ((e * d) % phi != 1)
                    return (false, "e và d không phải là nghịch đảo modulo", null!, null!, null!);
                
                // Create JSON format keys that only use e, n, d
                var publicKeyJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "e", e.ToString() },
                    { "n", n.ToString() }
                });
                
                var privateKeyJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "d", d.ToString() },
                    { "n", n.ToString() }
                });
                
                return (true, "Tạo khóa thành công", publicKeyJson, privateKeyJson, null!);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi tạo khóa: {ex.Message}", null!, null!, null!);
            }
        }

        public async Task<(bool Success, string Message, string Signature)> SignDataFromParamsAsync(ManualSignParamsModel model)
        {
            if (string.IsNullOrEmpty(model.Data))
                return (false, "Dữ liệu là null hoặc trống", null!);
                
            try
            {
                // Parse the parameters
                BigInteger e, n, d;
                
                try
                {
                    e = BigInteger.Parse(model.E);
                    d = BigInteger.Parse(model.D);
                    
                    // Calculate n from p and q if provided, otherwise use n directly
                    if (!string.IsNullOrEmpty(model.P) && !string.IsNullOrEmpty(model.Q))
                    {
                        var p = BigInteger.Parse(model.P);
                        var q = BigInteger.Parse(model.Q);
                        n = p * q;
                    }
                    else if (!string.IsNullOrEmpty(model.N))
                    {
                        n = BigInteger.Parse(model.N);
                    }
                    else
                    {
                        return (false, "Cần cung cấp (p,q) hoặc n", null!);
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Lỗi phân tích tham số: {ex.Message}", null!);
                }
                
                // Use the manual signing method
                return await SignWithNEDAsync(n.ToString(), e.ToString(), d.ToString(), model.Data, model.HashAlgorithm);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi ký: {ex.Message}", null!);
            }
        }

        public async Task<(bool Success, string Message, string PublicKey, string PrivateKey, string Signature)> AutoSignDataAsync(ManualSignAutoModel model)
        {
            if (string.IsNullOrEmpty(model.Data))
                return (false, "Dữ liệu là null hoặc trống", null!, null!, null!);
                
            if (model.KeySize < 2048)
                return (false, "Kích thước khóa phải ít nhất 2048 bit", null!, null!, null!);
                
            try
            {
                using var rsa = RSA.Create(model.KeySize);
                var parameters = rsa.ExportParameters(true);
                
                // Convert parameters to BigInteger
                var p = new BigInteger(parameters.P!, true, true);
                var q = new BigInteger(parameters.Q!, true, true);
                var e = new BigInteger(parameters.Exponent!, true, true);
                var d = new BigInteger(parameters.D!, true, true);
                var n = p * q;
                
                // Create JSON format keys that only use e, n, d
                var publicKeyJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "e", e.ToString() },
                    { "n", n.ToString() }
                });
                
                var privateKeyJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "d", d.ToString() },
                    { "n", n.ToString() }
                });
                
                // Hash the data
                byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(model.Data);
                byte[] hashBytes;
                
                if (model.HashAlgorithm?.ToUpper() == "SHA512")
                {
                    using var sha512 = SHA512.Create();
                    hashBytes = sha512.ComputeHash(dataBytes);
                }
                else
                {
                    using var sha256 = SHA256.Create();
                    hashBytes = sha256.ComputeHash(dataBytes);
                }
                
                // Convert hash to BigInteger (ensure positive)
                var hashInt = new BigInteger(hashBytes, isUnsigned: true);
                
                // Ensure the hash is smaller than the modulus
                hashInt = hashInt % n;
                
                // Perform manual RSA signing: sig = hash^d mod n
                var signature = BigInteger.ModPow(hashInt, d, n);
                
                // Convert signature to Base64 string
                byte[] sigBytes = signature.ToByteArray();
                Array.Reverse(sigBytes); // Convert to big-endian
                var base64Signature = Convert.ToBase64String(sigBytes);
                
                return (true, "Tạo khóa và ký thành công", publicKeyJson, privateKeyJson, base64Signature);
            }
            catch (Exception ex)
            {
                return (false, $"Error in AutoSignData: {ex.Message}", null!, null!, null!);
            }
        }

        public async Task<(bool Success, string Message, string SignId)> SaveKeyPairAsync(SaveKeyPairModel model)
        {
            try
            {
                string publicKey = model.PublicKey;
                string privateKey = model.PrivateKey;

                // Check if the key is in JSON format
                bool isJsonFormat = model.PublicKey.StartsWith("{") && model.PrivateKey.StartsWith("{");
                
                if (isJsonFormat)
                {
                    // Process JSON format keys
                    publicKey = ProcessJsonKey(model.PublicKey, true);
                    privateKey = ProcessJsonKey(model.PrivateKey, false);
                }

                // Validate the keys
                if (!ValidateKeys(ref publicKey, ref privateKey))
                {
                    return (false, "Định dạng khóa không hợp lệ", null!);
                }
                
                // Save to database
                var record = new Sign { 
                    PublicKey = publicKey, 
                    PrivateKey = privateKey, 
                    CreatedAt = DateTime.UtcNow, 
                    SignatureName = model.SignatureName, 
                    SignatureType = model.SignatureType, 
                    IsActive = true,
                    UserId = model.UserId,
                    P = model.P,
                    Q = model.Q,
                    E = model.E,
                    D = model.D
                };
                
                await _context.Signs.InsertOneAsync(record);
                return (true, "Keys saved successfully", record.Id);
            }
            catch (Exception ex)
            {
                return (false, $"Error saving keys: {ex.Message}", null!);
            }
        }

        public async Task<(bool Success, string Message, string E, string D)> GenerateEDFromPQAsync(GenerateEDFromPQModel model)
        {
            try
            {
                var pBI = BigInteger.Parse(model.P);
                var qBI = BigInteger.Parse(model.Q);
                
                if (pBI < 2 || qBI < 2)
                    return (false, "Tham số p và q phải >= 2", null!, null!);
                
                var phi = (pBI - 1) * (qBI - 1);
                
                // Use 65537 as default e value (common practice)
                BigInteger eBI = 65537;
                
                // If e is too large for phi, use a smaller value
                if (eBI >= phi)
                {
                    eBI = 3;
                }
                
                // Ensure e and phi are coprime
                if (BigInteger.GreatestCommonDivisor(eBI, phi) != 1)
                {
                    eBI = 3;
                    while (BigInteger.GreatestCommonDivisor(eBI, phi) != 1)
                    {
                        eBI += 2;
                        if (eBI >= phi) 
                        {
                            return (false, "Không thể tìm giá trị e hợp lệ. Hãy thử các giá trị p, q lớn hơn.", null!, null!);
                        }
                    }
                }
                
                // Calculate d as modular multiplicative inverse of e modulo phi
                var dBI = ModInverse(eBI, phi);
                
                return (true, "Tạo giá trị e,d thành công", eBI.ToString(), dBI.ToString());
            }
            catch (Exception ex)
            {
                return (false, $"Error generating e,d: {ex.Message}", null!, null!);
            }
        }
        
        public async Task<(bool Success, string Message, Sign Signature)> GetKeyDetailsAsync(string userId, string signId)
        {
            try
            {
                var filter = Builders<Sign>.Filter.And(
                    Builders<Sign>.Filter.Eq(s => s.UserId, userId),
                    Builders<Sign>.Filter.Eq(s => s.Id, signId)
                );
                
                var signature = await _context.Signs.Find(filter).FirstOrDefaultAsync();
                
                if (signature == null)
                {
                    return (false, "Không tìm thấy chữ ký", null!);
                }
                
                return (true, "Đã tìm thấy chữ ký", signature);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi lấy thông tin khóa: {ex.Message}", null!);
            }
        }

        public async Task<(bool Success, string Message, string SignId)> UpdateKeyPairAsync(UpdateKeyPairModel model)
        {
            try
            {
                string publicKey = model.PublicKey;
                string privateKey = model.PrivateKey;

                // Process JSON format keys if needed
                bool isJsonFormat = model.PublicKey.StartsWith("{") && model.PrivateKey.StartsWith("{");
                if (isJsonFormat)
                {
                    publicKey = ProcessJsonKey(model.PublicKey, true);
                    privateKey = ProcessJsonKey(model.PrivateKey, false);
                }

                // Validate the keys
                if (!ValidateKeys(ref publicKey, ref privateKey))
                {
                    return (false, "Định dạng khóa không hợp lệ", null!);
                }
                
                // Update key in database
                var filter = Builders<Sign>.Filter.And(
                    Builders<Sign>.Filter.Eq(s => s.UserId, model.UserId),
                    Builders<Sign>.Filter.Eq(s => s.Id, model.SignId)
                );
                
                var update = Builders<Sign>.Update
                    .Set(s => s.PublicKey, publicKey)
                    .Set(s => s.PrivateKey, privateKey)
                    .Set(s => s.SignatureName, model.SignatureName)
                    .Set(s => s.SignatureType, model.SignatureType)
                    .Set(s => s.UpdatedAt, DateTime.UtcNow);
                
                var result = await _context.Signs.UpdateOneAsync(filter, update);
                
                if (result.ModifiedCount == 0)
                {
                    return (false, "Không có chữ ký nào được cập nhật. Không tìm thấy chữ ký hoặc không có thay đổi.", null!);
                }
                
                return (true, "Keys updated successfully", model.SignId);
            }
            catch (Exception ex)
            {
                return (false, $"Error updating keys: {ex.Message}", null!);
            }
        }

        public async Task<(bool Success, string Message, string P, string Q, string E, string D)> GenerateParamsAsync(int keySize)
        {
            if (keySize < 2048)
                return (false, "Kích thước khóa phải ít nhất 2048 bit", null!, null!, null!, null!);

            try
            {
                // Generate RSA parameters
                using var rsa = RSA.Create(keySize);
                var parameters = rsa.ExportParameters(true);
                
                // Convert parameters to BigInteger
                var p = new BigInteger(parameters.P!, true, true);
                var q = new BigInteger(parameters.Q!, true, true);
                var e = new BigInteger(parameters.Exponent!, true, true);
                var d = new BigInteger(parameters.D!, true, true);
                
                return (true, "Parameters generated successfully", 
                    p.ToString(), 
                    q.ToString(), 
                    e.ToString(), 
                    d.ToString());
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi tạo tham số: {ex.Message}", null!, null!, null!, null!);
            }
        }

        public async Task<(bool Success, string Message, string PublicKey, string PrivateKey)> ImportKeysFromFilesAsync(ManualImportKeysModel model)
        {
            try
            {
                // Check if the content is provided
                if (string.IsNullOrEmpty(model.KeyFileContent))
                {
                    return (false, "Không có nội dung tệp khóa", null!, null!);
                }
                
                try
                {
                    // Parse the JSON content
                    using JsonDocument document = JsonDocument.Parse(model.KeyFileContent);
                    JsonElement root = document.RootElement;
                    
                    // Extract required keys
                    if (!root.TryGetProperty("publicKey", out var publicKeyElement) || 
                        !root.TryGetProperty("privateKey", out var privateKeyElement))
                    {
                        return (false, "Khóa công khai không hợp lệ - thiếu tham số e hoặc n", null!, null!);
                    }
                    
                    string publicKey = publicKeyElement.GetString() ?? "";
                    string privateKey = privateKeyElement.GetString() ?? "";
                    
                    if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
                    {
                        return (false, "Khóa công khai không hợp lệ - thiếu tham số e hoặc n", null!, null!);
                    }
                    
                    // Log the extracted keys for debugging
                    Console.WriteLine($"Extracted publicKey: {publicKey.Substring(0, Math.Min(20, publicKey.Length))}...");
                    Console.WriteLine($"Extracted privateKey: {privateKey.Substring(0, Math.Min(20, privateKey.Length))}...");
                    
                    // Extract additional parameters if available
                    string p = "", q = "", e = "", d = "", n = "";
                    
                    if (root.TryGetProperty("p", out var pElement))
                        p = pElement.GetString() ?? "";
                        
                    if (root.TryGetProperty("q", out var qElement))
                        q = qElement.GetString() ?? "";
                        
                    if (root.TryGetProperty("e", out var eElement))
                        e = eElement.GetString() ?? "";
                        
                    if (root.TryGetProperty("d", out var dElement))
                        d = dElement.GetString() ?? "";
                    
                    // Validate the keys
                    try
                    {
                        if (publicKey.StartsWith("{") && privateKey.StartsWith("{"))
                        {
                            // JSON format keys - try to parse them
                            try {
                                var publicKeyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(publicKey);
                                var privateKeyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(privateKey);
                                
                                // Check if they have the required fields
                                if (publicKeyObj == null || !publicKeyObj.ContainsKey("e") || !publicKeyObj.ContainsKey("n"))
                                {
                                    return (false, "Khóa công khai không hợp lệ - thiếu tham số e hoặc n", null!, null!);
                                }
                                
                                if (privateKeyObj == null || !privateKeyObj.ContainsKey("d") || !privateKeyObj.ContainsKey("n"))
                                {
                                    return (false, "Khóa riêng không hợp lệ - thiếu tham số d hoặc n", null!, null!);
                                }
                                
                                // Extract n, e, d from the keys for mathematical validation
                                if (publicKeyObj.TryGetValue("e", out e) && publicKeyObj.TryGetValue("n", out n) && 
                                    privateKeyObj.TryGetValue("d", out d) && privateKeyObj.TryGetValue("n", out var privateN))
                                {
                                    // Check if n values match
                                    if (n != privateN)
                                    {
                                        return (false, "Modulus (n) trong khóa công khai và riêng không khớp", null!, null!);
                                    }
                                    
                                    // Validate RSA mathematical relationship
                                    try
                                    {
                                        // Parse the parameters
                                        BigInteger nBig = BigInteger.Parse(n);
                                        BigInteger eBig = BigInteger.Parse(e);
                                        BigInteger dBig = BigInteger.Parse(d);
                                        
                                        // Validate key sizes
                                        if (nBig < 3 || eBig < 3 || dBig < 3)
                                        {
                                            return (false, "Tham số RSA không hợp lệ: n, e, và d phải >= 3", null!, null!);
                                        }
                                        
                                        // If we have p and q, validate them
                                        if (!string.IsNullOrEmpty(p) && !string.IsNullOrEmpty(q))
                                        {
                                            BigInteger pBig = BigInteger.Parse(p);
                                            BigInteger qBig = BigInteger.Parse(q);
                                            
                                            // Check if p and q are prime (simple check)
                                            if (!IsProbablyPrime(pBig) || !IsProbablyPrime(qBig))
                                            {
                                                return (false, "Tham số p và q phải là số nguyên tố", null!, null!);
                                            }
                                            
                                            // Check if n = p*q
                                            if (pBig * qBig != nBig)
                                            {
                                                return (false, "Tích của p và q không bằng n", null!, null!);
                                            }
                                            
                                            // Calculate phi(n) = (p-1)*(q-1)
                                            BigInteger phi = (pBig - 1) * (qBig - 1);
                                            
                                            // Check if e and phi are coprime
                                            if (BigInteger.GreatestCommonDivisor(eBig, phi) != 1)
                                            {
                                                return (false, "e và phi(n) không nguyên tố cùng nhau", null!, null!);
                                            }
                                            
                                            // Check if e*d ≡ 1 (mod phi(n))
                                            if ((eBig * dBig) % phi != 1)
                                            {
                                                return (false, "e và d không phải là nghịch đảo modulo phi(n)", null!, null!);
                                            }
                                        }
                                        else
                                        {
                                            // Basic validation: check if e*d mod n produces expected results
                                            // Test with a small number (e.g., 2)
                                            BigInteger testValue = 2;
                                            BigInteger encrypted = BigInteger.ModPow(testValue, eBig, nBig);
                                            BigInteger decrypted = BigInteger.ModPow(encrypted, dBig, nBig);
                                            
                                            if (decrypted != testValue)
                                            {
                                                return (false, "Khóa không hợp lệ, e và d không phải là nghịch đảo modulo phi(n)", null!, null!);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        return (false, $"Lỗi khi xác thực khóa: {ex.Message}", null!, null!);
                                    }
                                }
                                
                                // Keys are valid JSON format
                                Console.WriteLine("Thành công xác thực khóa");
                            }
                            catch (JsonException ex) {
                                return (false, $"Lỗi khi xác thực khóa: {ex.Message}", null!, null!);
                            }
                        }
                        else
                        {
                            // Try to validate base64 format
                            try {
                                var publicKeyBytes = Convert.FromBase64String(publicKey);
                                var privateKeyBytes = Convert.FromBase64String(privateKey);
                                
                                using var rsa = RSA.Create();
                                rsa.ImportRSAPublicKey(publicKeyBytes, out _);
                                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                                
                                // Additional validation for standard RSA keys
                                var rsaParams = rsa.ExportParameters(true);
                                
                                // Check if parameters are valid
                                if (rsaParams.Modulus == null || rsaParams.Exponent == null || 
                                    rsaParams.D == null || rsaParams.P == null || rsaParams.Q == null)
                                {
                                    return (false, "Khoá không hợp lệ", null!, null!);
                                }
                                
                                // Test encryption/decryption
                                byte[] testData = new byte[] { 1, 2, 3, 4, 5 };
                                byte[] encrypted;
                                
                                try
                                {
                                    encrypted = rsa.Encrypt(testData, RSAEncryptionPadding.Pkcs1);
                                    byte[] decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
                                    
                                    // Compare original and decrypted data
                                    if (!testData.SequenceEqual(decrypted))
                                    {
                                        return (false, "Xác thực khóa RSA không thành công: kiểm tra mã hóa/giải mã không thành công", null!, null!);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    return (false, $"Xác thực khóa RSA không thành công trong quá trình kiểm tra mã hóa: {ex.Message}", null!, null!);
                                }
                                
                                Console.WriteLine("Successfully validated Base64 format keys");
                            }
                            catch (Exception ex) {
                                return (false, $"Định dạng khóa không hợp lệ: {ex.Message}", null!, null!);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Lỗi xác thực khóa: {ex.Message}", null!, null!);
                    }
                    
                    // Log success
                    Console.WriteLine("Keys imported successfully");
                    
                    return (true, "Keys imported successfully", publicKey, privateKey);
                }
                catch (JsonException ex)
                {
                    return (false, $"Invalid JSON format in key file: {ex.Message}", null!, null!);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi nhập khóa: {ex.Message}", null!, null!);
            }
        }

        /// <summary>
        /// Sign data manually using only n, e, d parameters without using RSA class
        /// </summary>
        public async Task<(bool Success, string Message, string Signature)> SignWithNEDAsync(string n, string e, string d, string data, string hashAlgorithm = "SHA256")
        {
            try
            {
                if (string.IsNullOrEmpty(data))
                    return (false, "Dữ liệu là null hoặc trống", null!);
                
                // Parse the parameters
                BigInteger nBI, eBI, dBI;
                
                try
                {
                    nBI = BigInteger.Parse(n);
                    eBI = BigInteger.Parse(e);
                    dBI = BigInteger.Parse(d);
                }
                catch (Exception ex)
                {
                    return (false, $"Lỗi phân tích tham số: {ex.Message}", null!);
                }
                
                // Validate parameters
                if (nBI < 3 || eBI < 3 || dBI < 3)
                    return (false, "Tham số không hợp lệ: n, e, và d phải >= 3", null!);
                
                // Hash the data
                byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
                byte[] hashBytes;
                
                if (hashAlgorithm?.ToUpper() == "SHA512")
                {
                    using var sha512 = SHA512.Create();
                    hashBytes = sha512.ComputeHash(dataBytes);
                }
                else
                {
                    using var sha256 = SHA256.Create();
                    hashBytes = sha256.ComputeHash(dataBytes);
                }
                
                // Convert hash to BigInteger (ensure positive)
                var hashInt = new BigInteger(hashBytes, isUnsigned: true);
                
                // Ensure the hash is smaller than the modulus
                hashInt = hashInt % nBI;
                
                // Perform manual RSA signing: sig = hash^d mod n
                var signature = BigInteger.ModPow(hashInt, dBI, nBI);
                
                // Convert signature to Base64 string
                byte[] sigBytes = signature.ToByteArray();
                Array.Reverse(sigBytes); // Convert to big-endian
                var base64Signature = Convert.ToBase64String(sigBytes);
                
                return (true, "Data signed successfully using manual RSA", base64Signature);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi ký thủ công: {ex.Message}", null!);
            }
        }

        /// <summary>
        /// Sign a file using RSA signature and return the signature
        /// </summary>
        public async Task<(bool Success, string Message, string Signature, string FileName)> SignFileAsync(
            IFormFile file, string userId, string signatureId, string hashAlgorithm = "SHA256", 
            string privateKey = "", string n = "", string e = "", string d = "")
        {
            try
            {
                // Log to structured logger
                _logger.LogInformation($"Ký file: {file.FileName}");

                // Direct terminal output for critical errors
                if (file == null || file.Length == 0)
                {
                    string errorMsg = "LỖI: Cần có tệp để ký";
                    _errorOutput.WriteLine(errorMsg);
                    _logger.LogError(errorMsg);
                    return (false, errorMsg, null!, null!);
                }
                
                Console.WriteLine($"Signing file: {file.FileName}, userId: {userId}, signatureId: {signatureId}, hashAlgorithm: {hashAlgorithm}");
                
                // Read the file contents
                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }
                
                // Hash the file content
                byte[] hashBytes;
                if (hashAlgorithm?.ToUpper() == "SHA512")
                {
                    using var sha512 = SHA512.Create();
                    hashBytes = sha512.ComputeHash(fileBytes);
                }
                else
                {
                    using var sha256 = SHA256.Create();
                    hashBytes = sha256.ComputeHash(fileBytes);
                }
                
                // Determine signing method based on provided parameters
                string signature;
                
                // Method 1: Use signature from database if signatureId is provided
                if (!string.IsNullOrEmpty(signatureId))
                {
                    Console.WriteLine($"Using signature from database with ID: {signatureId}");
                    var keyResult = await GetKeyDetailsAsync(userId, signatureId);
                    if (!keyResult.Success || keyResult.Signature == null)
                    {
                        return (false, $"Lỗi khi lấy chữ ký: {keyResult.Message}", null!, null!);
                    }
                    
                    var sign = keyResult.Signature;
                    Console.WriteLine($"Retrieved signature: {sign.SignatureName}, type: {sign.SignatureType}");
                    
                    // Check if it has e, n, d parameters (manual key)
                    if (!string.IsNullOrEmpty(sign.E) && !string.IsNullOrEmpty(sign.N) && !string.IsNullOrEmpty(sign.D))
                    {
                        try
                        {
                            Console.WriteLine("Using manual signing with e, n, d parameters");
                            // Use manual signing with n, e, d
                            var hashInt = new BigInteger(hashBytes, isUnsigned: true);
                            var nBI = BigInteger.Parse(sign.N ?? "0");
                            var dBI = BigInteger.Parse(sign.D ?? "0");
                            
                            // Ensure the hash is smaller than the modulus
                            hashInt = hashInt % nBI;
                            
                            // Perform manual RSA signing: sig = hash^d mod n
                            var signatureBI = BigInteger.ModPow(hashInt, dBI, nBI);
                            
                            // Convert signature to Base64 string
                            byte[] sigBytes = signatureBI.ToByteArray();
                            Array.Reverse(sigBytes); // Convert to big-endian
                            signature = Convert.ToBase64String(sigBytes);
                            
                            return (true, "Tệp được ký thành công với các tham số thủ công", signature, file.FileName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error signing with manual parameters: {ex.Message}");
                            return (false, $"Lỗi ký với các tham số thủ công: {ex.Message}", null!, null!);
                        }
                    }
                    // Check if it's a JSON format key
                    else if (sign.PrivateKey?.StartsWith("{") == true && sign.PrivateKey?.EndsWith("}") == true)
                    {
                        try
                        {
                            Console.WriteLine("Using JSON format key");
                            // Try to parse as JSON format with d, n
                            var keyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(sign.PrivateKey);
                            
                            if (keyObj != null && keyObj.ContainsKey("d") && keyObj.ContainsKey("n"))
                            {
                                // Use manual signing with n, e, d
                                var hashInt = new BigInteger(hashBytes, isUnsigned: true);
                                var nBI = BigInteger.Parse(keyObj["n"]);
                                var dBI = BigInteger.Parse(keyObj["d"]);
                                
                                // Ensure the hash is smaller than the modulus
                                hashInt = hashInt % nBI;
                                
                                // Perform manual RSA signing: sig = hash^d mod n
                                var signatureBI = BigInteger.ModPow(hashInt, dBI, nBI);
                                
                                // Convert signature to Base64 string
                                byte[] sigBytes = signatureBI.ToByteArray();
                                Array.Reverse(sigBytes); // Convert to big-endian
                                signature = Convert.ToBase64String(sigBytes);
                                
                                return (true, "File signed successfully with JSON key", signature, file.FileName);
                            }
                            else
                            {
                                return (false, "Định dạng khóa riêng không hợp lệ", null!, null!);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Lỗi phân tích khóa JSON: {ex.Message}");
                            return (false, $"Lỗi phân tích khóa JSON: {ex.Message}", null!, null!);
                        }
                    }
                    else if (!string.IsNullOrEmpty(sign.PrivateKey))
                    {
                        try
                        {
                            Console.WriteLine("Using standard RSA with Base64 private key");
                            // Use standard RSA with the Base64 private key
                            using var rsa = RSA.Create();
                            var privateKeyBytes = Convert.FromBase64String(sign.PrivateKey);
                            rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                            
                            var rsaHashAlgo = hashAlgorithm?.ToUpper() == "SHA512" 
                                ? HashAlgorithmName.SHA512 
                                : HashAlgorithmName.SHA256;
                            
                            var signatureBytes = rsa.SignHash(hashBytes, rsaHashAlgo, RSASignaturePadding.Pkcs1);
                            signature = Convert.ToBase64String(signatureBytes);
                            
                            return (true, "File signed successfully with RSA", signature, file.FileName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error signing with RSA: {ex.Message}");
                            return (false, $"Lỗi ký bằng RSA: {ex.Message}", null!, null!);
                        }
                    }
                    else
                    {
                        return (false, "Không tìm thấy khóa hợp lệ trong hồ sơ chữ ký", null!, null!);
                    }
                }
                // Method 2: Use provided private key directly
                else if (!string.IsNullOrEmpty(privateKey))
                {
                    try
                    {
                        // Check if it's a JSON format key
                        if (privateKey.StartsWith("{") && privateKey.EndsWith("}"))
                        {
                            // Try to parse as JSON format with d, n
                            var keyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(privateKey);
                            
                            if (keyObj != null && keyObj.ContainsKey("d") && keyObj.ContainsKey("n"))
                            {
                                // Use manual signing with n, e, d
                                var hashInt = new BigInteger(hashBytes, isUnsigned: true);
                                var nBI = BigInteger.Parse(keyObj["n"]);
                                var dBI = BigInteger.Parse(keyObj["d"]);
                                
                                // Ensure the hash is smaller than the modulus
                                hashInt = hashInt % nBI;
                                
                                // Perform manual RSA signing: sig = hash^d mod n
                                var signatureBI = BigInteger.ModPow(hashInt, dBI, nBI);
                                
                                // Convert signature to Base64 string
                                byte[] sigBytes = signatureBI.ToByteArray();
                                Array.Reverse(sigBytes); // Convert to big-endian
                                signature = Convert.ToBase64String(sigBytes);
                            }
                            else
                            {
                                return (false, "Định dạng khóa riêng không hợp lệ", null!, null!);
                            }
                        }
                        else
                        {
                            // Use standard RSA with the Base64 private key
                            using var rsa = RSA.Create();
                            var privateKeyBytes = Convert.FromBase64String(privateKey);
                            rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                            
                            var rsaHashAlgo = hashAlgorithm?.ToUpper() == "SHA512" 
                                ? HashAlgorithmName.SHA512 
                                : HashAlgorithmName.SHA256;
                            
                            var signatureBytes = rsa.SignHash(hashBytes, rsaHashAlgo, RSASignaturePadding.Pkcs1);
                            signature = Convert.ToBase64String(signatureBytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Lỗi ký bằng khóa riêng: {ex.Message}", null!, null!);
                    }
                }
                // Method 3: Use manual n, e, d parameters
                else if (!string.IsNullOrEmpty(n) && !string.IsNullOrEmpty(d))
                {
                    try
                    {
                        // Use manual signing with n, e, d
                        var hashInt = new BigInteger(hashBytes, isUnsigned: true);
                        var nBI = BigInteger.Parse(n);
                        var dBI = BigInteger.Parse(d);
                        
                        // Ensure the hash is smaller than the modulus
                        hashInt = hashInt % nBI;
                        
                        // Perform manual RSA signing: sig = hash^d mod n
                        var signatureBI = BigInteger.ModPow(hashInt, dBI, nBI);
                        
                        // Convert signature to Base64 string
                        byte[] sigBytes = signatureBI.ToByteArray();
                        Array.Reverse(sigBytes); // Convert to big-endian
                        signature = Convert.ToBase64String(sigBytes);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Error signing with n, d parameters: {ex.Message}", null!, null!);
                    }
                }
                else
                {
                    return (false, "No valid signing method provided. Either signatureId, privateKey, or n,d must be provided", null!, null!);
                }
                
                return (false, "Failed to sign file with the provided parameters", null!, null!);
            }
            catch (Exception ex)
            {
                // Log to structured logger
                _logger.LogError(ex, "Error during file signing");

                // Direct terminal error output
                string errorMsg = $"TERMINAL ERROR: {ex.Message}";
                _errorOutput.WriteLine(errorMsg);

                return (false, errorMsg, null!, null!);
            }
        }

        /// <summary>
        /// Verify a file signature
        /// </summary>
        public async Task<(bool Success, string Message)> VerifyFileSignatureAsync(
    IFormFile file, string signature, string publicKey, string hashAlgorithm = "SHA256")
    {
        try
        {
            if (file == null || file.Length == 0)
                return (false, "Tệp chữ ký là bắt buộc");

            if (string.IsNullOrEmpty(signature))
                return (false, "Chữ ký là bắt buộc");

            if (string.IsNullOrEmpty(publicKey))
                return (false, "Khóa công khai là bắt buộc");

            // Kiểm tra định dạng Base64 của chữ ký
            if (!IsValidBase64(signature))
                return (false, "Chữ ký không đúng định dạng Base64");

            // Đọc nội dung file
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            // Băm nội dung file
            byte[] hashBytes;
            if (hashAlgorithm?.ToUpper() == "SHA512")
            {
                using var sha512 = SHA512.Create();
                hashBytes = sha512.ComputeHash(fileBytes);
            }
            else
            {
                using var sha256 = SHA256.Create();
                hashBytes = sha256.ComputeHash(fileBytes);
            }

            // Kiểm tra khóa công khai JSON
            if (publicKey.StartsWith("{") && publicKey.EndsWith("}"))
            {
                try
                {
                    var keyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(publicKey);
                    if (keyObj == null || !keyObj.ContainsKey("e") || !keyObj.ContainsKey("n"))
                        return (false, $"Định dạng khóa công khai không hợp lệ: thiếu {(keyObj?.ContainsKey("e") == true ? "n" : "e")}");

                    // Xác thực thủ công với e, n
                    var hashInt = new BigInteger(hashBytes, isUnsigned: true);
                    var nBI = BigInteger.Parse(keyObj["n"]);
                    var eBI = BigInteger.Parse(keyObj["e"]);
                    hashInt = hashInt % nBI;

                    byte[] signatureBytes = Convert.FromBase64String(signature);
                    Array.Reverse(signatureBytes);
                    var signatureBI = new BigInteger(signatureBytes, isUnsigned: true);

                    var decryptedHash = BigInteger.ModPow(signatureBI, eBI, nBI);
                    bool isValid = decryptedHash.Equals(hashInt);

                    return isValid
                        ? (true, "Chữ ký tệp hợp lệ")
                        : (false, $"Chữ ký tệp không hợp lệ. Giá trị băm hiện tại: {Convert.ToBase64String(hashBytes)}");
                }
                catch (Exception ex)
                {
                    return (false, $"Lỗi phân tích khóa JSON: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    // Xác thực với khóa Base64
                    using var rsa = RSA.Create();
                    var publicKeyBytes = Convert.FromBase64String(publicKey);
                    rsa.ImportRSAPublicKey(publicKeyBytes, out _);

                    var signatureBytes = Convert.FromBase64String(signature);
                    var rsaHashAlgo = hashAlgorithm?.ToUpper() == "SHA512"
                        ? HashAlgorithmName.SHA512
                        : HashAlgorithmName.SHA256;

                    bool isValid = rsa.VerifyHash(hashBytes, signatureBytes, rsaHashAlgo, RSASignaturePadding.Pkcs1);

                    return isValid
                        ? (true, "Chữ ký tệp hợp lệ")
                        : (false, $"Chữ ký tệp không hợp lệ. Giá trị băm hiện tại: {Convert.ToBase64String(hashBytes)}");
                }
                catch (Exception ex)
                {
                    return (false, $"Lỗi xác thực bằng RSA: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi xác thực chữ ký tệp: {ex.Message}");
        }
    }

    // Thêm hàm IsValidBase64 ngay dưới VerifyFileSignatureAsync
    private bool IsValidBase64(string str)
    {
        try
        {
            Convert.FromBase64String(str);
            return true;
        }
        catch
        {
            return false;
        }
    }

        #region Private Helper Methods

        /// <summary>
        /// Create RSA object from p, q, e, d parameters
        /// This method is essential for manual key generation on the frontend
        /// </summary>
        private (bool Success, string Message, RSA Rsa) CreateRsaFromParams(string pStr, string qStr, string eStr, string dStr)
        {
            try
            {
                var pBI = BigInteger.Parse(pStr);
                var qBI = BigInteger.Parse(qStr);
                var eBI = BigInteger.Parse(eStr);
                var dBI = BigInteger.Parse(dStr);
                
                if (pBI < 2 || qBI < 2)
                    return (false, "Tham số p và q phải >= 2", null!);
                    
                var n = pBI * qBI;
                var phi = (pBI - 1) * (qBI - 1);
                
                if (BigInteger.GreatestCommonDivisor(eBI, phi) != 1)
                    return (false, "e and phi(n) are not coprime", null!);
                    
                if ((eBI * dBI) % phi != 1)
                    return (false, "e and d are not modular inverses", null!);
                    
                var dp = dBI % (pBI - 1);
                var dq = dBI % (qBI - 1);
                
                var inverseQ = ModInverse(qBI, pBI);
                
                var parameters = new RSAParameters
                {
                    Modulus = ToBigEndian(n),
                    Exponent = ToBigEndian(eBI),
                    D = ToBigEndian(dBI),
                    P = ToBigEndian(pBI),
                    Q = ToBigEndian(qBI),
                    DP = ToBigEndian(dp),
                    DQ = ToBigEndian(dq),
                    InverseQ = ToBigEndian(inverseQ)
                };
                
                var rsa = RSA.Create();
                rsa.ImportParameters(parameters);
                return (true, string.Empty, rsa);
            }
            catch (Exception ex)
            {
                return (false, $"Error creating RSA from parameters: {ex.Message}", null!);
            }
        }

        /// <summary>
        /// Calculate modular multiplicative inverse
        /// </summary>
        private BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            BigInteger m0 = m, x0 = 0, x1 = 1;
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
        
        /// <summary>
        /// Convert BigInteger to big-endian byte array
        /// </summary>
        private byte[] ToBigEndian(BigInteger v)
        {
            var bs = v.ToByteArray();
            if (bs[^1] == 0) Array.Resize(ref bs, bs.Length - 1);
            Array.Reverse(bs);
            return bs;
        }
        
        /// <summary>
        /// Process JSON format keys
        /// </summary>
        private string ProcessJsonKey(string jsonKey, bool isPublic)
        {
            try
            {
                // Check if it contains the RSA key directly
                if (jsonKey.Contains("rsaPublicKey") || jsonKey.Contains("rsaPrivateKey"))
                {
                    var keyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonKey);
                    string keyField = isPublic ? "rsaPublicKey" : "rsaPrivateKey";
                    
                    if (keyObj != null && keyObj.ContainsKey(keyField))
                    {
                        return keyObj[keyField];
                    }
                }
                else 
                {
                    // For manual/auto keys in (e,n) and (d,n) format
                    var keyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonKey);
                    
                    if (keyObj != null)
                    {
                        // For keys in (e,n) and (d,n) format, preserve the format
                        // The frontend needs the JSON format for displaying and editing
                        if ((isPublic && keyObj.ContainsKey("e") && keyObj.ContainsKey("n")) ||
                            (!isPublic && keyObj.ContainsKey("d") && keyObj.ContainsKey("n")))
                        {
                            // Just return the original JSON for (e,n) and (d,n) format
                            return jsonKey;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Continue with original value if processing fails
            }
            
            return jsonKey;
        }
        
        /// <summary>
        /// Validate and possibly regenerate keys
        /// </summary>
        private bool ValidateKeys(ref string publicKey, ref string privateKey)
        {
            // If keys are in JSON format, keep them as is
            if ((publicKey.StartsWith("{") && publicKey.EndsWith("}")) &&
                (privateKey.StartsWith("{") && privateKey.EndsWith("}")))
            {
                try
                {
                    // Try to parse to verify it's valid JSON
                    var pubKeyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(publicKey);
                    var privKeyObj = JsonSerializer.Deserialize<Dictionary<string, string>>(privateKey);
                    
                    if (pubKeyObj != null && privKeyObj != null)
                    {
                        // For keys in (e,n) and (d,n) format
                        if (pubKeyObj.ContainsKey("e") && pubKeyObj.ContainsKey("n") &&
                            privKeyObj.ContainsKey("d") && privKeyObj.ContainsKey("n"))
                        {
                            return true; // JSON format (e,n) and (d,n) is valid
                        }
                        
                        // For keys with rsaPublicKey and rsaPrivateKey fields
                        if (pubKeyObj.ContainsKey("rsaPublicKey") && privKeyObj.ContainsKey("rsaPrivateKey"))
                        {
                            return true; // JSON format with rsaPublicKey/rsaPrivateKey is valid
                        }
                    }
                }
                catch
                {
                    // Invalid JSON, continue to try Base64 format
                }
            }
            
            // Try to validate as Base64 RSA keys
            try
            {
                var publicKeyBytes = Convert.FromBase64String(publicKey);
                var privateKeyBytes = Convert.FromBase64String(privateKey);
                
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(publicKeyBytes, out _);
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                
                return true; // Valid Base64 RSA keys
            }
            catch
            {
                // If validation fails, create new keys
                try
                {
                    using var newRsa = RSA.Create(2048);
                    publicKey = Convert.ToBase64String(newRsa.ExportRSAPublicKey());
                    privateKey = Convert.ToBase64String(newRsa.ExportRSAPrivateKey());
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        // Helper method to check if a number is probably prime
        private bool IsProbablyPrime(BigInteger n)
        {
            if (n <= 1) return false;
            if (n <= 3) return true;
            if (n % 2 == 0 || n % 3 == 0) return false;
            
            // Simple primality test for small numbers
            for (int i = 5; i * i <= n; i += 6)
            {
                if (n % i == 0 || n % (i + 2) == 0)
                    return false;
            }
            
            return true;
        }
        
        #endregion
    }
}