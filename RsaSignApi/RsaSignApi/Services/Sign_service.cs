// File: Services/SignService.cs
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using GroupDocs.Signature;
using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Options;
using iTextSharp.text.pdf;
using Org.BouncyCastle.Security;
using System.Security.Cryptography.Pkcs;

namespace RsaSignApi.Services
{
    public interface IKyTapTin
    {
        Task<byte[]> KyAsync(Stream inputStream, X509Certificate2 cert, string fileName);
    }

    public interface IXacMinhChuKy
    {
        Task<KetQuaXacMinh> XacMinhAsync(Stream inputStream, string fileName);
    }

    public class KyTapTinGroupDocs : IKyTapTin
    {
        public async Task<byte[]> KyAsync(Stream inputStream, X509Certificate2 cert, string fileName)
        {
            if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));
            if (cert == null) throw new ArgumentNullException(nameof(cert));

            using var msIn = new MemoryStream();
            await inputStream.CopyToAsync(msIn);
            msIn.Position = 0;

            var pfxBytes = cert.Export(X509ContentType.Pkcs12);
            using var certStream = new MemoryStream(pfxBytes);

            using var signature = new Signature(msIn);
            var options = new DigitalSignOptions
            {
                CertificateStream = certStream,
                Password = "",
                Reason = "Tài liệu đã được ký số",
                Location = "Vietnam",
                Contact = "RsaSignApi",
                Visible = Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            };

            if (options.Visible)
            {
                options.Left = 36;
                options.Top = 36;
                options.Width = 200;
                options.Height = 100;
            }

            using var msOut = new MemoryStream();
            signature.Sign(msOut, options);
            return msOut.ToArray();
        }
    }

    public class XacMinhChuKyDaDefinh : IXacMinhChuKy
    {
        public async Task<KetQuaXacMinh> XacMinhAsync(Stream inputStream, string fileName)
        {
            if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await inputStream.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var result = new KetQuaXacMinh();
            try
            {
                if (ext == ".pdf")
                    result = await XacMinhPdfAsync(fileBytes);
                else if (new[] { ".docx", ".xlsx", ".pptx" }.Contains(ext))
                    result = await XacMinhOfficeAsync(fileBytes);
                else if (ext == ".p7s")
                    result = await XacMinhP7sAsync(fileBytes);
                else
                {
                    result.ThanhCong = false;
                    result.ThongBaoLoi = "Định dạng file không được hỗ trợ";
                }
            }
            catch (Exception ex)
            {
                result.ThanhCong = false;
                result.ThongBaoLoi = $"Lỗi xác minh: {ex.Message}";
            }

            return result;
        }

        private Task<KetQuaXacMinh> XacMinhPdfAsync(byte[] fileBytes)
        {
            var ketQua = new KetQuaXacMinh();
            using var reader = new PdfReader(fileBytes);
            var acro = reader.AcroFields;
            var sigNames = acro.GetSignatureNames();

            if (sigNames.Any())
            {
                var pkcs7 = acro.VerifySignature(sigNames[0]);
                ketQua.HopLe = pkcs7.Verify();
                ketQua.ThanhCong = true;

                if (ketQua.HopLe)
                {
                    var bcCert = pkcs7.SigningCertificate;
                    var dotnet = new X509Certificate2(DotNetUtilities.ToX509Certificate(bcCert));
                    ketQua.TenNguoiKy = dotnet.GetNameInfo(X509NameType.SimpleName, false);
                    ketQua.EmailNguoiKy = dotnet.GetNameInfo(X509NameType.EmailName, false);
                    ketQua.ThoiGianKy = pkcs7.SignDate.ToUniversalTime();
                }
            }
            else
            {
                ketQua.ThongBaoLoi = "Không tìm thấy chữ ký trong PDF";
            }

            return Task.FromResult(ketQua);
        }

        private Task<KetQuaXacMinh> XacMinhOfficeAsync(byte[] fileBytes)
        {
            var ketQua = new KetQuaXacMinh { ThanhCong = true };
            using var signature = new Signature(new MemoryStream(fileBytes));
            var result = signature.Verify(new DigitalVerifyOptions());

            ketQua.HopLe = result.IsValid;
            if (ketQua.HopLe && result.Succeeded.FirstOrDefault() is DigitalSignature ds)
            {
                ketQua.TenNguoiKy = ds.Comments ?? "Không xác định";
                ketQua.ThoiGianKy = ds.SignTime;
                if (ds.Certificate != null)
                {
                    try
                    {
                        // đ ã là X509Certificate2
                        ketQua.TenNguoiKy = ds.Certificate.GetNameInfo(X509NameType.SimpleName, false);
                        ketQua.EmailNguoiKy = ds.Certificate.GetNameInfo(X509NameType.EmailName, false);
                    }
                    catch { /* ignore */ }
                }
            }

            return Task.FromResult(ketQua);
        }

        private Task<KetQuaXacMinh> XacMinhP7sAsync(byte[] fileBytes)
        {
            var ketQua = new KetQuaXacMinh();
            var cms = new SignedCms();
            cms.Decode(fileBytes);

            try
            {
                cms.CheckSignature(true);
                ketQua.HopLe = true;
                ketQua.ThanhCong = true;

                var si = cms.SignerInfos[0];
                var cert = si.Certificate;
                ketQua.TenNguoiKy = cert.GetNameInfo(X509NameType.SimpleName, false);
                ketQua.EmailNguoiKy = cert.GetNameInfo(X509NameType.EmailName, false);
                ketQua.ThoiGianKy = si.SignedAttributes
                                     .OfType<Pkcs9SigningTime>()
                                     .FirstOrDefault()?.SigningTime.ToUniversalTime();
            }
            catch (Exception ex)
            {
                ketQua.HopLe = false;
                ketQua.ThongBaoLoi = $"Chữ ký P7S không hợp lệ: {ex.Message}";
            }

            return Task.FromResult(ketQua);
        }
    }

    public class DichVuKyTapTin
    {
        private readonly X509Certificate2 _cert;
        private readonly IKyTapTin _impl;

        public DichVuKyTapTin(X509Certificate2 cert)
        {
            _cert = cert ?? throw new ArgumentNullException(nameof(cert));
            _impl = new KyTapTinGroupDocs();
        }

        public async Task<(byte[] Data, string Extension)> KyTheoLoaiAsync(Stream inputStream, string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var signed = await _impl.KyAsync(inputStream, _cert, fileName);
            var outExt = new[] { ".pdf", ".docx", ".xlsx", ".pptx" }.Contains(ext) ? ext : ".p7s";
            return (signed, outExt);
        }
    }

    public class DichVuXacMinhChuKy
    {
        private readonly IXacMinhChuKy _impl;
        public DichVuXacMinhChuKy() => _impl = new XacMinhChuKyDaDefinh();
        public Task<KetQuaXacMinh> XacMinhTheoLoaiAsync(Stream s, string f) => _impl.XacMinhAsync(s, f);
    }

    public class KetQuaXacMinh
    {
        public bool ThanhCong { get; set; } = false;
        public bool HopLe { get; set; } = false;
        public string TenNguoiKy { get; set; }
        public string EmailNguoiKy { get; set; }
        public DateTime? ThoiGianKy { get; set; }
        public string ThongBaoLoi { get; set; }
        public string LoaiTapTin { get; set; }
        public string TenTapTin { get; set; }
    }
}
