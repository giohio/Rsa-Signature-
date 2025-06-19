using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RsaSignApi.Model.RequestModels
{
    /// <summary>
    /// Model for manual generation of RSA keys.
    /// </summary>
    public class ManualGenerateKeyModel
    {
        [Range(2048, int.MaxValue, ErrorMessage = "Key size must be at least 2048 bits")]
        public int KeySize { get; set; } = 2048;
    }

    /// <summary>
    /// Model for manual data signing.
    /// </summary>
    public class ManualSignDataModel
    {
        [Required]
        public string Data { get; set; }

        [Required]
        public string PrivateKey { get; set; }
    }

    /// <summary>
    /// Model for saving generated key pair to database
    /// </summary>
    public class SaveKeyPairModel
    {
        /// <summary>
        /// The public key (Base64 or JSON format)
        /// </summary>
        public string PublicKey { get; set; } = string.Empty;
        
        /// <summary>
        /// The private key (Base64 or JSON format)
        /// </summary>
        public string PrivateKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Name for the signature
        /// </summary>
        public string SignatureName { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of signature (e.g., "RSA")
        /// </summary>
        public string SignatureType { get; set; } = "RSA";
        
        /// <summary>
        /// User ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional p parameter
        /// </summary>
        public string P { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional q parameter
        /// </summary>
        public string Q { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional e parameter
        /// </summary>
        public string E { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional d parameter
        /// </summary>
        public string D { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model for verifying raw data signatures.
    /// </summary>
    public class ManualVerifyModel
    {
        [Required]
        public string Data { get; set; }

        [Required]
        public string Signature { get; set; }

        [Required]
        public string PublicKey { get; set; }
    }

    /// <summary>
    /// Model for manual signing parameters p,q,e,d and data.
    /// </summary>
    public class ManualSignParamsModel
    {
        /// <summary>
        /// The prime number p (optional if n is provided)
        /// </summary>
        public string P { get; set; } = string.Empty;
        
        /// <summary>
        /// The prime number q (optional if n is provided)
        /// </summary>
        public string Q { get; set; } = string.Empty;
        
        /// <summary>
        /// The public exponent e
        /// </summary>
        [Required]
        public string E { get; set; } = string.Empty;
        
        /// <summary>
        /// The private exponent d
        /// </summary>
        [Required]
        public string D { get; set; } = string.Empty;
        
        /// <summary>
        /// The modulus n (optional if p and q are provided)
        /// </summary>
        public string N { get; set; } = string.Empty;
        
        /// <summary>
        /// The data to sign
        /// </summary>
        [Required]
        public string Data { get; set; } = string.Empty;
        
        /// <summary>
        /// Name of the signature
        /// </summary>
        [Required]
        public string SignatureName { get; set; } = string.Empty;
        
        /// <summary>
        /// User ID for the signature
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Hash algorithm to use (SHA256 or SHA512)
        /// </summary>
        public string HashAlgorithm { get; set; } = "SHA256";
    }

    /// <summary>
    /// Model for automatic signing: generates keys and signature.
    /// </summary>
    public class ManualSignAutoModel
    {
        /// <summary>
        /// The data to sign
        /// </summary>
        [Required]
        public string Data { get; set; } = string.Empty;
        
        /// <summary>
        /// Key size in bits (minimum 2048)
        /// </summary>
        [Range(2048, 8192, ErrorMessage = "Key size must be at least 2048 bits")]
        public int KeySize { get; set; } = 2048;
        
        /// <summary>
        /// Name of the signature
        /// </summary>
        [Required]
        public string SignatureName { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of the signature
        /// </summary>
        [Required]
        public string SignatureType { get; set; } = "Auto";
        
        /// <summary>
        /// User ID for the signature
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Hash algorithm to use (SHA256 or SHA512)
        /// </summary>
        public string HashAlgorithm { get; set; } = "SHA256";
    }

    /// <summary>
    /// Model for exporting public and private keys to files.
    /// </summary>
    public class ManualExportKeysModel
    {
        public string UserId { get; set; }
        public string SignId { get; set; }
        public string Format { get; set; } = "json"; // Default format
    }

    /// <summary>
    /// Model for importing keys from files
    /// </summary>
    public class ManualImportKeysModel
    {
        /// <summary>
        /// Content of the key file (JSON format)
        /// </summary>
        [Required]
        public string KeyFileContent { get; set; } = string.Empty;
        
        [Required]
        public string UserId { get; set; }
        
        [Required]
        public string SignatureName { get; set; }
        
        [Required]
        public string SignatureType { get; set; } = "Imported";
    }

    /// <summary>
    /// Model for verifying a signed PDF document.
    /// </summary>
    public class ManualVerifySignatureModel
    {
        public IFormFile? File { get; set; }
    }

    /// <summary>
    /// Model for generating e,d values from p,q.
    /// </summary>
    public class GenerateEDFromPQModel
    {
        /// <summary>
        /// The prime number p
        /// </summary>
        [Required]
        public string P { get; set; } = string.Empty;
        
        /// <summary>
        /// The prime number q
        /// </summary>
        [Required]
        public string Q { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model for updating an existing key pair
    /// </summary>
    public class UpdateKeyPairModel
    {
        /// <summary>
        /// The signature ID to update
        /// </summary>
        public string SignId { get; set; } = string.Empty;
        
        /// <summary>
        /// The public key (Base64 or JSON format)
        /// </summary>
        public string PublicKey { get; set; } = string.Empty;
        
        /// <summary>
        /// The private key (Base64 or JSON format)
        /// </summary>
        public string PrivateKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Name for the signature
        /// </summary>
        public string SignatureName { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of signature (e.g., "RSA")
        /// </summary>
        public string SignatureType { get; set; } = "RSA";
        
        /// <summary>
        /// User ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model for signing with n, e, d parameters directly
    /// </summary>
    public class SignWithNEDModel
    {
        [Required]
        public string N { get; set; }
        
        [Required]
        public string E { get; set; }
        
        [Required]
        public string D { get; set; }
        
        [Required]
        public string Data { get; set; }
        
        public string UserId { get; set; }
        
        public string SignatureName { get; set; }
        
        public string HashAlgorithm { get; set; } = "SHA256";
    }
    
    /// <summary>
    /// Model for signing with private key in Base64 format
    /// </summary>
    public class SignWithPrivateKeyModel
    {
        [Required]
        public string PrivateKey { get; set; }
        
        [Required]
        public string Data { get; set; }
        
        public string UserId { get; set; }
        
        public string SignatureName { get; set; }
        
        public string HashAlgorithm { get; set; } = "SHA256";
    }

    /// <summary>
    /// Model for signing a file with RSA parameters or key.
    /// </summary>
    public class SignFileModel
    {
        /// <summary>
        /// The file to sign
        /// </summary>
        [Required]
        public IFormFile File { get; set; }
        
        /// <summary>
        /// Signature ID to use for signing
        /// </summary>
        public string SignatureId { get; set; } = string.Empty;
        
        /// <summary>
        /// User ID
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Hash algorithm to use (SHA256 or SHA512)
        /// </summary>
        public string HashAlgorithm { get; set; } = "SHA256";
        
        /// <summary>
        /// Optional private key to use directly
        /// </summary>
        public string PrivateKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional parameters for manual signing (n, e, d)
        /// </summary>
        public string N { get; set; } = string.Empty;
        public string E { get; set; } = string.Empty;
        public string D { get; set; } = string.Empty;
    }
}
