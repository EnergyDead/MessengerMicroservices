using System.Security.Claims;

namespace UserService.Services;

public interface ITokenGenerator
{
    string GenerateToken(IEnumerable<Claim> claims);
}