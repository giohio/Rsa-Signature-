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
                return BadRequest("Username already exists");

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
            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginModel model)
        {
            var user = await _context.Users.Find(u => u.Username == model.Username).FirstOrDefaultAsync();
            if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
                return Unauthorized("Invalid username or password");

            return Ok(new { message = "Login successful", userId = user.Id, fullName = user.FullName });
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
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
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