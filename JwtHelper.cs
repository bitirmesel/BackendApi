using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

public static class JwtHelper
{
    public static string GenerateJwtToken(
        long userId,
        string name,
        string email,
        string role,
        string issuer,
        string audience,
        SymmetricSecurityKey signingKey)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub,  userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("name", name),
            new Claim(ClaimTypes.Role, role)
        };

        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(4),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
