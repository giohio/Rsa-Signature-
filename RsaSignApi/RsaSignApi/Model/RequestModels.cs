using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RsaSignApi.Model.RequestModels
{
    /// <summary>
    /// Model for generating RSA key pairs.
    /// </summary>
    public class GenerateKeyModel
    {
        /// <summary>User identifier.</summary>
        public string? UserId { get; set; }

        /// <summary>Key size in bits (min 2048).</summary>
        public int KeySize { get; set; } = 2048;

        /// <summary>Name of the signature.</summary>
        [Required]
        public string SignatureName { get; set; }

        /// <summary>Type of the signature.</summary>
        [Required]
        public string SignatureType { get; set; }
    }

    /// <summary>
    /// Model for signing documents (PDF, DOCX, XLSX, PPTX, etc.).
    /// </summary>
    public class SignDocumentModel
    {
        /// <summary>User identifier.</summary>
        [FromForm(Name = "userid")]
        [Required]
        public string? UserId { get; set; }

        /// <summary>Signature record identifier.</summary>
        [FromForm(Name = "signid")]
        [Required]
        public string? SignId { get; set; }

        /// <summary>Uploaded file to be signed.</summary>
        [FromForm(Name = "file")]
        [Required]
        public IFormFile? File { get; set; }

        /// <summary>
        /// Hash algorithm to use for signing (SHA256 or SHA512).
        /// </summary>
        [FromForm(Name = "hashalgorithm")]
        public string? HashAlgorithm { get; set; } = "SHA256";

        /// <summary>
        /// Indicates whether the signature is manual.
        /// </summary>
        public bool IsManualSignature { get; set; }

        /// <summary>
        /// Whether to use embedded signature (PDF with digital signature inside)
        /// </summary>
        [FromForm(Name = "useembeddedsign")]
        public bool UseEmbeddedSign { get; set; } = true;

        /// <summary>
        /// RSA modulus (n).
        /// </summary>
        public string? N { get; set; }

        /// <summary>
        /// RSA public exponent (e).
        /// </summary>
        public string? E { get; set; }

        /// <summary>
        /// RSA private exponent (d).
        /// </summary>
        public string? D { get; set; }

        /// <summary>
        /// RSA prime factor p.
        /// </summary>
        public string? P { get; set; }

        /// <summary>
        /// RSA prime factor q.
        /// </summary>
        public string? Q { get; set; }
    }

    /// <summary>
    /// Model for verifying a signed document (only file required).
    /// </summary>
    public class VerifySignatureModel
    {
        /// <summary>Uploaded signed file.</summary>
        [FromForm(Name = "file")]
        public IFormFile? File { get; set; }
        
        /// <summary>Original file for detached signature verification.</summary>
        [FromForm(Name = "originalfile")]
        public IFormFile? OriginalFile { get; set; }
        
        /// <summary>Whether the signature is embedded in the document.</summary>
        [FromForm(Name = "isembedded")]
        public bool IsEmbedded { get; set; } = true;
    }
}
