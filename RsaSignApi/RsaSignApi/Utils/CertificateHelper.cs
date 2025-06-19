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
using Microsoft.Extensions.Logging;

namespace RsaSignApi.Utils
{
    public static class CertificateHelper
    {
        private static ILogger _logger;

        static CertificateHelper()
        {
            // Sử dụng ILoggerFactory.Create để tạo logger khi cần
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = loggerFactory.CreateLogger("CertificateHelper");
        }

        public static X509Certificate2 CreateCertificateFromKeyPair(AsymmetricCipherKeyPair keyPair, string subjectName)
        {
            _logger.LogInformation($"CreateCertificateFromKeyPair: Creating certificate for {subjectName}");
            
            try
            {
                var certificateGenerator = new X509V3CertificateGenerator();

                // Parse subjectName to handle special characters
                var attributes = new List<DerObjectIdentifier>();
                var values = new List<string>();
                
                // Split subjectName into components
                var components = subjectName.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim().Split('=', 2))
                    .Where(kv => kv.Length == 2);

                foreach (var kv in components)
                {
                    var key = kv[0].Trim();
                    var value = kv[1].Trim();
                    
                    if (key.Equals("CN", StringComparison.OrdinalIgnoreCase))
                    {
                        attributes.Add(X509Name.CN);
                        values.Add(value);
                    }
                    else if (key.Equals("E", StringComparison.OrdinalIgnoreCase))
                    {
                        attributes.Add(X509Name.E);
                        values.Add(value);
                    }
                }

                if (!attributes.Any())
                {
                    _logger.LogError("No valid subject attributes provided");
                    throw new ArgumentException("Invalid subjectName format");
                }

                var subject = new X509Name(attributes, values);
                var issuer = subject; // Self-signed
                _logger.LogDebug($"Parsed subject: {subject.ToString()}");

                var serialNumber = BigInteger.ValueOf(DateTime.UtcNow.Ticks);
                var notBefore = DateTime.UtcNow.AddDays(-1);
                var notAfter = DateTime.UtcNow.AddYears(1);

                _logger.LogDebug($"Certificate details: Subject={subject.ToString()}, Validity={notBefore} to {notAfter}, SerialNumber={serialNumber}");

                certificateGenerator.SetSerialNumber(serialNumber);
                certificateGenerator.SetIssuerDN(issuer);
                certificateGenerator.SetSubjectDN(subject);
                certificateGenerator.SetNotBefore(notBefore);
                certificateGenerator.SetNotAfter(notAfter);
                
                // Set public key
                try
                {
                    certificateGenerator.SetPublicKey(keyPair.Public);
                    _logger.LogDebug("Public key set to certificate generator");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error setting public key: {ex.Message}");
                    throw new Exception("Failed to set public key for certificate", ex);
                }

                // Add extensions
                try
                {
                    certificateGenerator.AddExtension(
                        X509Extensions.BasicConstraints,
                        false,
                        new BasicConstraints(false));

                    certificateGenerator.AddExtension(
                        X509Extensions.KeyUsage,
                        true,
                        new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

                    // Add Subject Alternative Name for email
                    if (values.Any(v => attributes[values.IndexOf(v)] == X509Name.E))
                    {
                        var email = values[attributes.IndexOf(X509Name.E)];
                        var san = new GeneralNames(new GeneralName(GeneralName.Rfc822Name, email));
                        certificateGenerator.AddExtension(
                            X509Extensions.SubjectAlternativeName,
                            false,
                            san);
                        _logger.LogDebug($"Added Subject Alternative Name: {email}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error adding certificate extensions: {ex.Message}");
                    throw new Exception("Failed to add certificate extensions", ex);
                }

                // Create signature factory
                Asn1SignatureFactory signatureFactory;
                try
                {
                    signatureFactory = new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private);
                    _logger.LogDebug("Signature factory created");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating signature factory: {ex.Message}");
                    throw new Exception("Failed to create signature factory", ex);
                }

                // Generate the certificate
                Org.BouncyCastle.X509.X509Certificate certificate;
                try
                {
                    certificate = certificateGenerator.Generate(signatureFactory);
                    _logger.LogDebug("BouncyCastle certificate generated successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error generating certificate: {ex.Message}");
                    throw new Exception("Failed to generate certificate", ex);
                }

                // Convert to .NET X509Certificate2
                X509Certificate2 x509Certificate2;
                try
                {
                    x509Certificate2 = new X509Certificate2(certificate.GetEncoded());
                    _logger.LogDebug("Converted to X509Certificate2 successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error converting to X509Certificate2: {ex.Message}");
                    throw new Exception("Failed to convert to X509Certificate2", ex);
                }

                // Combine with private key
                try
                {
                    var rsa = RSA.Create();
                    
                    if (keyPair.Private is RsaPrivateCrtKeyParameters rsaParams)
                    {
                        _logger.LogDebug("Importing RSA parameters from BouncyCastle key");
                        rsa.ImportParameters(DotNetUtilities.ToRSAParameters(rsaParams));
                    }
                    else
                    {
                        _logger.LogError("Private key is not RsaPrivateCrtKeyParameters");
                        throw new Exception("Private key is not in the expected format");
                    }
                    
                    var certWithKey = x509Certificate2.CopyWithPrivateKey(rsa);
                    _logger.LogInformation("Certificate created successfully with private key");
                    return certWithKey;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error combining certificate with private key: {ex.Message}");
                    throw new Exception("Failed to combine certificate with private key", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Certificate creation failed: {ex.Message}");
                if (ex.InnerException != null)
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                throw;
            }
        }
    }
}