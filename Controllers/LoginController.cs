using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using projetoX.Data;
using projetoX.Models;

namespace projetoX.Controllers;

[Route("api/login")]
[AllowAnonymous]
[ApiController]
public class LoginController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public LoginController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost]
    public IActionResult Login([FromBody] Login login)
    {
        var usuario = _context.Usuarios.FirstOrDefault(u => u.Email == login.Email);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(login.Senha, usuario.SenhaHash))
            return Unauthorized("Usuário ou senha inválidos.");

        var accessToken = GenerateJwtToken(usuario);
        var refreshToken = GenerateRefreshToken(usuario.Email);

        _context.RefreshTokens.Add(refreshToken);
        _context.SaveChanges();

        return Ok(new { Token = accessToken, RefreshToken = refreshToken.Token });
    }

    [HttpPost("refresh")]
    public IActionResult RefreshToken([FromBody] string refreshToken)
    {
        var tokenExistente = _context.RefreshTokens.FirstOrDefault(rt =>
            rt.Token == refreshToken && rt.Ativo
        );

        if (tokenExistente == null || tokenExistente.Expiration < DateTime.UtcNow)
            return Unauthorized("Refresh token inválido ou expirado.");

        var usuario = _context.Usuarios.FirstOrDefault(u => u.Email == tokenExistente.EmailUsuario);
        if (usuario == null)
            return Unauthorized("Usuário não encontrado.");

        var novoAccessToken = GenerateJwtToken(usuario);

        tokenExistente.Ativo = false;
        var novoRefreshToken = GenerateRefreshToken(usuario.Email);
        _context.RefreshTokens.Add(novoRefreshToken);
        _context.SaveChanges();

        return Ok(new { Token = novoAccessToken, RefreshToken = novoRefreshToken.Token });
    }

    private string GenerateJwtToken(Usuario usuario)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // Token curto
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private RefreshToken GenerateRefreshToken(string email)
    {
        return new RefreshToken
        {
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            EmailUsuario = email,
            Expiration = DateTime.UtcNow.AddDays(7),
            Ativo = true,
        };
    }
}
