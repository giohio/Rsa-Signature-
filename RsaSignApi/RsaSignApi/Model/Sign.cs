using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RsaSignApi.Model
{
    public class Sign
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("userId")]
        public string UserId { get; set; } = null!;

        [BsonElement("publicKey")]
        public string PublicKey { get; set; } = null!;    // Base64 của RSAPublicKey (PKCS#1)

        [BsonElement("privateKey")]
        public string PrivateKey { get; set; } = null!;   // Base64 của RSAPrivateKey (PKCS#1)

        [BsonElement("documentHash")]
        public string? DocumentHash { get; set; }          // Base64 của SHA256(PDF signed)

        [BsonElement("email")]
        public string? Email { get; set; }

        [BsonElement("fullName")]
        public string? FullName { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }
        
        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("signatureName")]
        public string SignatureName { get; set; } = null!;

        [BsonElement("signatureType")]
        public string SignatureType { get; set; } = null!;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;
        
        [BsonElement("p")]
        public string? P { get; set; }
        
        [BsonElement("q")]
        public string? Q { get; set; }
        
        [BsonElement("e")]
        public string? E { get; set; }
        
        [BsonElement("d")]
        public string? D { get; set; }
        
        [BsonElement("n")]
        public string? N { get; set; }
    }
    public class CertificateEntity
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        // Nếu bạn lưu raw CER/DER ở đây:
        public byte[]? Data { get; set; }

        // Nếu bạn lưu PFX (có thể kèm mật khẩu) ở đây:
        public byte[]? PfxData { get; set; }

        // Mật khẩu của PFX (nếu có)
        public string? Password { get; set; }
    }
}
