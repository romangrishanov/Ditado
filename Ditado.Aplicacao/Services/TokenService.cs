using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ditado.Dominio.Entidades;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Ditado.Aplicacao.Services;

public class TokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GerarToken(Usuario usuario)
    {
        var chaveSecreta = _configuration["Jwt:ChaveSecreta"] 
            ?? throw new InvalidOperationException("Chave secreta JWT não configurada");
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveSecreta));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, usuario.Tipo.ToString()),
            new Claim("Login", usuario.Login)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Emissor"],
            audience: _configuration["Jwt:Audiencia"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}