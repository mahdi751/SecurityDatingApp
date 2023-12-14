using System.Security.Claims;
using API.Entities;

namespace API.Interfaces;

public interface ITokenService
{
    Task<string> CreateToken(AppUser user);
    string CreateRefreshToken();
    ClaimsPrincipal GetPrincipleFromExpiredToken(string token);
}