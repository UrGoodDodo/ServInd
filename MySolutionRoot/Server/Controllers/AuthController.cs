using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Data;
using Server.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using Server.Dtos;
using System.Threading.Tasks;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext db, IWebHostEnvironment env, IConfiguration config)
        {
            _db = db;
            _env = env;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (await _db.Users.AnyAsync(u => u.Nickname == req.Nickname))
            {
                return BadRequest(new RegisterResponse
                {
                    Success = false,
                    Message = "Никнейм уже занят.",
                    Nickname = req.Nickname
                });
            }

            string hash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            string extension = Path.GetExtension(req.AvatarFilename).ToLower();
            string fileName = $"{Guid.NewGuid()}{extension}";
            string avatarDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "avatars");
            Directory.CreateDirectory(avatarDir);
            string fullPath = Path.Combine(avatarDir, fileName);

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(req.AvatarBase64);
                await System.IO.File.WriteAllBytesAsync(fullPath, bytes);
            }
            catch
            {
                return BadRequest(new RegisterResponse
                {
                    Success = false,
                    Message = "Невалидное содержимое аватара.",
                    Nickname = req.Nickname
                });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Nickname = req.Nickname,
                PasswordHash = hash,
                AvatarUrl = $"/avatars/{fileName}"
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new RegisterResponse
            {
                Success = true,
                Message = "Пользователь успешно зарегистрирован.",
                Nickname = user.Nickname,
                AvatarBase64 = req.AvatarBase64,
                AvatarExtension = extension
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Nickname == req.Nickname);
            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            {
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Message = "Неверный никнейм или пароль."
                });
            }

            // Создание JWT
            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Nickname),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Чтение аватарки и кодирование в Base64
            string avatarBase64 = "";
            string extension = "";

            try
            {
                string fullPath = Path.Combine(_env.WebRootPath ?? "wwwroot", user.AvatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                extension = Path.GetExtension(fullPath);
                if (System.IO.File.Exists(fullPath))
                {
                    byte[] avatarBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                    avatarBase64 = Convert.ToBase64String(avatarBytes);
                }
            }
            catch
            {
                // Игнорируем ошибку, просто не заполняем аватар
            }

            return Ok(new LoginResponse
            {
                Success = true,
                Message = "Успешный вход.",
                UserId = user.Id,
                Nickname = user.Nickname,
                AvatarBase64 = avatarBase64,
                AvatarExtension = extension,
                JwtToken = tokenString
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new
            {
                Success = true,
                Message = "Logout complete, token no longer works on client"
            });
        }
    }
}
