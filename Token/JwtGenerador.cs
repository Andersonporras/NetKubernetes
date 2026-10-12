using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NetKubernetes.Models;
using Microsoft.IdentityModel.Tokens;

namespace NetKubernetes.Token;

public class JwtGenerador : IJwtGenerador
{
    private readonly IConfiguration _configuracion;

    public JwtGenerador(IConfiguration configuracion)
    {
        _configuracion = configuracion;
    }

    public string CrearToken(Usuario usuario)
    {
        var claims = new List<Claim> {
            new Claim(JwtRegisteredClaimNames.NameId, usuario.UserName!),
            new Claim("userId", usuario.Id),
            new Claim("email", usuario.Email!)
        };

        // La misma clave que valida Program.cs. Nunca se escribe en el codigo:
        // llega por variable de entorno Jwt__Key o por user-secrets.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuracion["Jwt:Key"]!));
        var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var tokenDescripcion = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddDays(30),
            SigningCredentials = credenciales
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescripcion);
        return tokenHandler.WriteToken(token);
    }
}
