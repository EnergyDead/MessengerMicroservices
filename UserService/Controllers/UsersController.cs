using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.Services;
using UserService.Utils;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDbContext _db, IConfiguration _configuration, ITokenGenerator generator) : ControllerBase
{
    [HttpGet("by-email/{email}")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> GetUserByEmail(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (user == null)
        {
            return NotFound("User with this email not found.");
        }

        return Ok(new UserResponse { Id = user.Id, Username = user.Username, Email = user.Email });
    }

    [HttpGet("{userId:guid}")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> GetUserById(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        return Ok(new UserResponse { Id = user.Id, Username = user.Username, Email = user.Email });
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
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, newUser.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, newUser.Id.ToString()),
            new(ClaimTypes.Name, newUser.Username),
            new(ClaimTypes.Email, newUser.Email)
        };
        var token = generator.GenerateToken(claims); 

        return Ok(new AuthResponse
        {
            UserId = newUser.Id,
            Username = newUser.Username,
            Email = newUser.Email,
            Token = token,
            Expires = DateTimeOffset.UtcNow.AddMinutes(GetJwtTokenLifetime())
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
        
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email)
        };
        var token = generator.GenerateToken(claims);

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Token = token,
            Expires = DateTimeOffset.UtcNow.AddMinutes(GetJwtTokenLifetime())
        });
    }

    private double GetJwtTokenLifetime()
    {
        return double.TryParse(_configuration["Jwt:TokenLifetimeMinutes"], out var lifetime) ? lifetime : 60;
    }
}