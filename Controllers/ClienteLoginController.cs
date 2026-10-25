using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using projetoX.Data;
using projetoX.Models;

namespace projetoX.Controllers;

[Route("api/cliente/login")]
[ApiController]
public class ClienteLoginController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClienteLoginController> _logger;

    public ClienteLoginController(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<ClienteLoginController> logger
    )
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult Login([FromBody] ClienteLogin login)
    {
        try
        {
            if (
                login == null
                || string.IsNullOrEmpty(login.Email)
                || string.IsNullOrEmpty(login.Senha)
            )
            {
                _logger.LogWarning(
                    "Tentativa de login com credenciais inválidas: email ou senha vazios."
                );
                return BadRequest("Email e senha são obrigatórios.");
            }

            // Buscar o cliente pelo email
            var cliente = _context.Clientes.FirstOrDefault(c => c.Email == login.Email);
            if (cliente == null)
            {
                _logger.LogWarning(
                    "Tentativa de login com email não encontrado: {Email}",
                    login.Email
                );
                return Unauthorized("Email ou senha inválidos.");
            }

            // Verificar a senha
            if (!BCrypt.Net.BCrypt.Verify(login.Senha, cliente.SenhaHash))
            {
                _logger.LogWarning(
                    "Tentativa de login com senha incorreta para email: {Email}",
                    login.Email
                );
                return Unauthorized("Email ou senha inválidos.");
            }

            // Gerar o token de acesso
            var accessToken = GenerateJwtToken(cliente);
            // Gerar o refresh token
            var refreshToken = GenerateRefreshToken(cliente.Email);

            // Adicionar o refresh token ao banco de dados
            _context.RefreshTokens.Add(refreshToken);
            _context.SaveChanges();

            // Retornar o token e o id do cliente
            return Ok(
                new
                {
                    Token = accessToken,
                    RefreshToken = refreshToken.Token,
                    ClienteId = cliente.Id,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar login para email: {Email}", login.Email);
            return StatusCode(500, "Erro interno no servidor.");
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public IActionResult RefreshToken([FromBody] string refreshToken)
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Tentativa de refresh com token vazio.");
                return BadRequest("Refresh token é obrigatório.");
            }

            var tokenExistente = _context.RefreshTokens.FirstOrDefault(rt =>
                rt.Token == refreshToken && rt.Ativo
            );
            if (tokenExistente == null || tokenExistente.Expiration < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token inválido ou expirado: {Token}", refreshToken);
                return Unauthorized("Refresh token inválido ou expirado.");
            }

            var cliente = _context.Clientes.FirstOrDefault(c =>
                c.Email == tokenExistente.EmailUsuario
            );
            if (cliente == null)
            {
                _logger.LogWarning(
                    "Cliente não encontrado para refresh token: {Token}",
                    refreshToken
                );
                return Unauthorized("Cliente não encontrado.");
            }

            var novoAccessToken = GenerateJwtToken(cliente);

            tokenExistente.Ativo = false;
            var novoRefreshToken = GenerateRefreshToken(cliente.Email);
            _context.RefreshTokens.Add(novoRefreshToken);
            _context.SaveChanges();

            return Ok(new { Token = novoAccessToken, RefreshToken = novoRefreshToken.Token });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar refresh token: {Token}", refreshToken);
            return StatusCode(500, "Erro interno no servidor.");
        }
    }

    private string GenerateJwtToken(Cliente cliente)
    {
        var secretKey =
            Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? _configuration["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(secretKey) || secretKey.Contains("${JWT_SECRET_KEY}"))
        {
            _logger.LogError("JWT SecretKey não configurada corretamente.");
            throw new InvalidOperationException("JWT SecretKey não configurada corretamente.");
        }

        var issuer =
            Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _configuration["Jwt:Issuer"];
        var audience =
            Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _configuration["Jwt:Audience"];

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, cliente.Id.ToString()),
            new Claim(ClaimTypes.Name, cliente.Nome),
            new Claim(ClaimTypes.Email, cliente.Email),
            new Claim(ClaimTypes.Role, "Cliente"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
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

public class ClienteLogin
{
    public string Email { get; set; }
    public string Senha { get; set; }
}
