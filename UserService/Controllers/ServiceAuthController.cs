using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using UserService.DTOs;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceAuthController(IConfiguration _configuration, ITokenGenerator generator) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("service-login")]
    public async Task<ActionResult<AuthResponse>> ServiceLogin(ServiceLoginRequest request)
    {
        var expectedSecret = _configuration[$"ServiceAuth:{request.ServiceName}:ServiceSecret"];
        if (string.IsNullOrEmpty(expectedSecret) || request.ServiceSecret != expectedSecret)
        {
            return Unauthorized("Неверные учетные данные сервиса или сервис не зарегистрирован.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, request.ServiceName),
            new("service_name", request.ServiceName)
        };

        var tokenString = generator.GenerateToken(claims);

        var jwtExpiresInMinutes = Convert.ToDouble(_configuration["Jwt:ExpiresInMinutes"] ?? "60");
        var expires = DateTime.UtcNow.AddMinutes(jwtExpiresInMinutes);

        return Ok(new AuthResponse
        {
            UserId = Guid.Empty,
            Username = request.ServiceName,
            Email = $"{request.ServiceName}@service.com",
            Token = tokenString,
            Expires = expires
        });
    }
}