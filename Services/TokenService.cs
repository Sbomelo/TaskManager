using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services
{
    public class TokenService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<AppUser> _hasher = new();

        public TokenService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<AppUser?> RegisterAsync (RegisterRequest request)
        {
            var username = request.Username.Trim().ToLowerInvariant();
            if(await _db.Users.AnyAsync(U => U.Username == username))
            {
                return null;
            }

            var user = new AppUser 
            {
                Username = username,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName
            };

            user.PasswordHash = _hasher.HashPassword(user, request.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;

        }

        public async Task<LoginResponse?> AuthenticateAsync(LoginRequest request)
        {
            var username = request.Username.Trim().ToLowerInvariant();
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user is null) return null;

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed) return null;

            return new LoginResponse(GenerateJwt(user), user.DisplayName);
        }
        private string GenerateJwt(AppUser user)
        {
            var secret = _config["Jwt:Secret"]
                ?? throw new InvalidOperationException("Set the Jwt__Secret environment variable.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };

            var token = new JwtSecurityToken(
                issuer: "meridian-taskboard",
                audience: "meridian-taskboard",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
