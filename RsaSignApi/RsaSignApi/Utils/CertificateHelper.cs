using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace RsaSignApi.Utils
{
    public static class CertificateHelper
    {
        public static X509Certificate2 CreateCertificateFromKeyPair(AsymmetricCipherKeyPair keyPair, string subjectName)
        {
            var certificateGenerator = new X509V3CertificateGenerator();

            // Basic certificate information
            var subject = new X509Name(subjectName);
            var issuer = subject; // Self-signed
            var serialNumber = BigInteger.ValueOf(DateTime.UtcNow.Ticks);
            var notBefore = DateTime.UtcNow.AddDays(-1);
            var notAfter = DateTime.UtcNow.AddYears(1);

            certificateGenerator.SetSerialNumber(serialNumber);
            certificateGenerator.SetIssuerDN(issuer);
            certificateGenerator.SetSubjectDN(subject);
            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);
            certificateGenerator.SetPublicKey(keyPair.Public);

            // Add basic extensions
            certificateGenerator.AddExtension(
                X509Extensions.BasicConstraints,
                false,
                new BasicConstraints(false));

            certificateGenerator.AddExtension(
                X509Extensions.KeyUsage,
                true,
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

            // Create signature factory
            var signatureFactory = new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private);

            // Generate the certificate
            var certificate = certificateGenerator.Generate(signatureFactory);

            // Convert to .NET X509Certificate2
            var x509Certificate2 = new X509Certificate2(certificate.GetEncoded());

            // Combine with private key
            var rsa = RSA.Create();
            rsa.ImportParameters(DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)keyPair.Private));

            return x509Certificate2.CopyWithPrivateKey(rsa);
        }
    }
}