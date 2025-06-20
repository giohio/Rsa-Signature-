using Microsoft.AspNetCore.Mvc;
using RsaSignApi.Data;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Driver; // Thêm namespace này
using MongoDB.Driver.Linq; // Thêm namespace này để hỗ trợ Find và FirstOrDefaultAsync
using RsaSignApi.Model;

namespace RsaSignApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public UserController(MongoDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingUser = await _context.Users.Find(u => u.Username == model.Username).FirstOrDefaultAsync();
            if (existingUser != null)
                return BadRequest("Tên đăng nhập đã tồn tại");

            var passwordHash = HashPassword(model.Password);
            var user = new User
            {
                Username = model.Username,
                PasswordHash = passwordHash,
                Email = model.Email,
                FullName = model.FullName,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Users.InsertOneAsync(user);
            return Ok(new { message = "Đăng ký người dùng thành công" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginModel model)
        {
            var user = await _context.Users.Find(u => u.Username == model.Username).FirstOrDefaultAsync();
            if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
                return Unauthorized("Tên đăng nhập hoặc mật khẩu không đúng");

            return Ok(new { message = "Đăng nhập thành công", userId = user.Id, fullName = user.FullName });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null) return NotFound();
            return Ok(new { userId = user.Id, username = user.Username, fullName = user.FullName, email = user.Email });
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
        }

        [HttpGet("check-libreoffice")]
        public IActionResult CheckLibreOffice()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "soffice",
                    Arguments = "--version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                string output = string.Empty;
                string error = string.Empty;
                
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null)
                    {
                        return BadRequest(new { status = "error", message = "Failed to start LibreOffice process" });
                    }
                    
                    output = p.StandardOutput.ReadToEnd();
                    error = p.StandardError.ReadToEnd();
                    
                    if (!p.WaitForExit(10_000) || p.ExitCode != 0)
                    {
                        return BadRequest(new { 
                            status = "error", 
                            message = $"LibreOffice check failed. Exit code: {p.ExitCode}", 
                            error = error,
                            output = output
                        });
                    }
                }
                
                // Check temp directory
                string tempDir = Environment.GetEnvironmentVariable("TMPDIR") ?? Path.GetTempPath();
                tempDir = string.IsNullOrEmpty(tempDir) ? "/tmp/libreoffice-conversion" : tempDir;
                
                bool tempDirExists = Directory.Exists(tempDir);
                bool tempDirWritable = false;
                
                if (tempDirExists)
                {
                    try
                    {
                        string testFile = Path.Combine(tempDir, $"test-{Guid.NewGuid()}.txt");
                        System.IO.File.WriteAllText(testFile, "Test");
                        System.IO.File.Delete(testFile);
                        tempDirWritable = true;
                    }
                    catch
                    {
                        tempDirWritable = false;
                    }
                }
                
                return Ok(new { 
                    status = "ok", 
                    libreOfficeVersion = output.Trim(),
                    tempDirectory = tempDir,
                    tempDirExists = tempDirExists,
                    tempDirWritable = tempDirWritable
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            var hash = HashPassword(password);
            return hash == storedHash;
        }
    }

    public class UserRegisterModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
    }

    public class UserLoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}