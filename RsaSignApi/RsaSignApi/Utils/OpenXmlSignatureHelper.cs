using System;
using System.Security.Cryptography.X509Certificates;

namespace RsaSignApi.Utils
{
    public static class OpenXmlSignatureHelper
    {
        /// <summary>
        /// Stub implementation for OpenXML document signing.
        /// </summary>
        public static byte[] SignOpenXml(byte[] inputBytes, X509Certificate2 certificate)
        {
            // TODO: Implement OpenXML signing or integrate Aspose/GroupDocs for OpenXML formats
            throw new NotImplementedException("OpenXML signing not implemented.");
        }
    }
}
