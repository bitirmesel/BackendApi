using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DktApi.Models.Db;
using Microsoft.IdentityModel.Tokens;

namespace DktApi;

public class JwtHelper
{
    private readonly IConfiguration _config;

    public JwtHelper(IConfiguration config)
    {
        _config = config;
    }

    // Artık DbUser değil, Therapist alıyor
    public string GenerateToken(Therapist therapist)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, therapist.Email ?? string.Empty),
            new Claim("therapistId", therapist.Id.ToString()),
            new Claim(ClaimTypes.Name, therapist.Name ?? string.Empty),
            new Claim(ClaimTypes.Role, "therapist")
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
