using Microsoft.AspNetCore.Http;

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
    }

    /// <summary>
    /// Model for signing documents (PDF, DOCX, XLSX, PPTX, etc.).
    /// </summary>
    public class SignDocumentModel
    {
        /// <summary>User identifier.</summary>
        public string? UserId { get; set; }

        /// <summary>Signature record identifier.</summary>
        public string? SignId { get; set; }

        /// <summary>Uploaded file to be signed.</summary>
        public IFormFile? File { get; set; }
    }

    /// <summary>
    /// Model for verifying a signed document (only file required).
    /// </summary>
    public class VerifySignatureModel
    {
        /// <summary>Uploaded signed file.</summary>
        public IFormFile? File { get; set; }
    }
}
