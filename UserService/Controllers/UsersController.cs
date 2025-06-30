using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.Utils;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDbContext _db, IConfiguration _configuration) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<User>> Get(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        return user is null ? NotFound() : Ok(user);
    }
    
    /// <summary>
    /// Регистрирует нового пользователя.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
        {
            return Conflict("Пользователь с таким Email уже существует.");
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = PasswordHasher.HashPassword(request.Password), // Хешируем пароль
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        // После регистрации сразу выдаем токен
        var token = GenerateJwtToken(newUser);

        return Ok(new AuthResponse
        {
            UserId = newUser.Id,
            Username = newUser.Username,
            Email = newUser.Email,
            Token = token,
            Expires = DateTimeOffset.UtcNow.AddMinutes(GetJwtTokenLifetime()) // Срок действия токена
        });
    }
    
    /// <summary>
    /// Аутентифицирует пользователя и выдает JWT-токен.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Неверный Email или пароль.");
        }

        var token = GenerateJwtToken(user);

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Token = token,
            Expires = DateTimeOffset.UtcNow.AddMinutes(GetJwtTokenLifetime())
        });
    }
    
    /// <summary>
    /// Генерирует JWT-токен для пользователя.
    /// </summary>
    private string GenerateJwtToken(User user)
    {
        var jwtSecret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured.");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "your-issuer";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "your-audience";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var tokenLifetimeMinutes = GetJwtTokenLifetime();

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(tokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private double GetJwtTokenLifetime()
    {
        return double.TryParse(_configuration["Jwt:TokenLifetimeMinutes"], out var lifetime) ? lifetime : 60;
    }
}