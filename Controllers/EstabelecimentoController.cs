using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projetoX.Data;
using projetoX.DTOs;
using projetoX.Models;
using projetoX.Services;

namespace projetoX.Controllers;

[Route("api/estabelecimentos")]
[Authorize]
[ApiController]
public class EstabelecimentoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TwilioService _twilioService;

    public EstabelecimentoController(AppDbContext context, TwilioService twilioService)
    {
        _context = context;
        _twilioService = twilioService;
    }

    // Buscar estabelecimentos por nome (autocompletar)
    [HttpGet("buscar")]
    [AllowAnonymous]
    public async Task<IActionResult> BuscarEstabelecimentos([FromQuery] string query)
    {
        if (string.IsNullOrEmpty(query))
            return BadRequest(new { mensagem = "A query de busca é obrigatória." });

        var estabelecimentos = await _context.Estabelecimentos
            .Where(e => e.Nome.Contains(query) && e.ProprietarioId == null)
            .Select(e => new EstabelecimentoDTO
            {
                PlaceId = e.PlaceId,
                Nome = e.Nome,
                FormattedAddress = e.FormattedAddress,
                Logradouro = e.Logradouro ?? "",
                Numero = e.Numero ?? "",
                Bairro = e.Bairro ?? "",
                Cidade = e.Cidade ?? "",
                Estado = e.Estado ?? "",
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                Telefone = e.Telefone ?? "",
                AbertoAgora = e.AbertoAgora,
                FotoUrl = e.Foto ?? "",
                Types = new List<string>(), // Inicializa vazio, será preenchido após
                Rating = e.Rating
            })
            .Take(10)
            .ToListAsync();

        // Processar Types no lado do cliente
        foreach (var est in estabelecimentos)
        {
            if (!string.IsNullOrEmpty(est.TypesString))
            {
                est.Types = est.TypesString.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        return Ok(estabelecimentos);
    }

    // Verificar telefone e enviar código SMS
    [HttpPost("verificar-telefone")]
    public async Task<IActionResult> VerificarTelefone([FromBody] VerificarTelefoneRequest request)
    {
        var clienteId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (clienteId == 0) return Unauthorized("Usuário não autenticado.");

        var estabelecimento = await _context.Estabelecimentos
            .FirstOrDefaultAsync(e => e.PlaceId == request.PlaceId);

        if (estabelecimento == null)
            return NotFound(new { mensagem = "Estabelecimento não encontrado." });

        if (estabelecimento.ProprietarioId != null)
            return BadRequest(new { mensagem = "Estabelecimento já possui um proprietário." });

        if (estabelecimento.Telefone != request.Telefone)
            return BadRequest(new { mensagem = "Telefone incorreto." });

        // Gerar código de 6 dígitos
        var code = new Random().Next(100000, 999999).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        var verificationCode = new VerificationCode
        {
            ClienteId = clienteId,
            EstabelecimentoPlaceId = request.PlaceId,
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsUsed = false
        };

        _context.VerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync();

        // Enviar SMS
        var message = $"Seu código de verificação para vincular ao estabelecimento {estabelecimento.Nome} é: {code}";
        await _twilioService.SendSmsAsync(estabelecimento.Telefone, message);

        return Ok(new { mensagem = "Código enviado por SMS." });
    }

    // Validar código e vincular
    [HttpPost("validar-codigo")]
    public async Task<IActionResult> ValidarCodigo([FromBody] ValidarCodigoRequest request)
    {
        var clienteId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (clienteId == 0) return Unauthorized("Usuário não autenticado.");

        var verificationCode = await _context.VerificationCodes
            .FirstOrDefaultAsync(v =>
                v.ClienteId == clienteId &&
                v.EstabelecimentoPlaceId == request.PlaceId &&
                v.Code == request.Code &&
                !v.IsUsed &&
                v.ExpiresAt > DateTime.UtcNow);

        if (verificationCode == null)
            return BadRequest(new { mensagem = "Código inválido ou expirado." });

        var estabelecimento = await _context.Estabelecimentos
            .FirstOrDefaultAsync(e => e.PlaceId == request.PlaceId);

        if (estabelecimento == null)
            return NotFound(new { mensagem = "Estabelecimento não encontrado." });

        if (estabelecimento.ProprietarioId != null)
            return BadRequest(new { mensagem = "Estabelecimento já possui um proprietário." });

        // Vincular o cliente ao estabelecimento
        estabelecimento.ProprietarioId = clienteId;
        verificationCode.IsUsed = true;
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Vinculação realizada com sucesso." });
    }

    // Listar estabelecimentos do cliente logado
    [HttpGet("meus")]
    public async Task<IActionResult> ListarMeusEstabelecimentos()
    {
        var clienteId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (clienteId == 0) return Unauthorized("Usuário não autenticado.");

        var estabelecimentos = await _context.Estabelecimentos
            .Where(e => e.ProprietarioId == clienteId)
            .Select(e => new EstabelecimentoDTO
            {
                PlaceId = e.PlaceId,
                Nome = e.Nome,
                FormattedAddress = e.FormattedAddress,
                Logradouro = e.Logradouro ?? "",
                Numero = e.Numero ?? "",
                Bairro = e.Bairro ?? "",
                Cidade = e.Cidade ?? "",
                Estado = e.Estado ?? "",
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                Telefone = e.Telefone ?? "",
                AbertoAgora = e.AbertoAgora,
                FotoUrl = e.Foto ?? "",
                Types = new List<string>(), // Inicializa vazio, será preenchido após
                Rating = e.Rating,
                Cep = e.Cep,
            })
            .ToListAsync();

        // Processar Types no lado do cliente
        foreach (var est in estabelecimentos)
        {
            if (!string.IsNullOrEmpty(est.TypesString))
            {
                est.Types = est.TypesString.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        return Ok(estabelecimentos);
    }

    [HttpPut("{placeId}")]
    public async Task<IActionResult> EditarEstabelecimento(string placeId, [FromBody] EstabelecimentoDTO dto)
    {
        var clienteId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (clienteId == 0) return Unauthorized("Usuário não autenticado.");

        var estabelecimento = await _context.Estabelecimentos
            .FirstOrDefaultAsync(e => e.PlaceId == placeId && e.ProprietarioId == clienteId);

        if (estabelecimento == null)
            return NotFound(new { mensagem = "Estabelecimento não encontrado ou você não é o proprietário." });

        // Atualizar os campos
        estabelecimento.Nome = dto.Nome;
        estabelecimento.FormattedAddress = dto.FormattedAddress;
        estabelecimento.Logradouro = dto.Logradouro;
        estabelecimento.Numero = dto.Numero;
        estabelecimento.Bairro = dto.Bairro;
        estabelecimento.Cidade = dto.Cidade;
        estabelecimento.Estado = dto.Estado;
        estabelecimento.Cep = dto.Cep; // Atualizar o campo Cep
        estabelecimento.Telefone = dto.Telefone;
        estabelecimento.Foto = dto.FotoUrl;
        estabelecimento.UltimaAtualizacao = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { mensagem = "Estabelecimento atualizado com sucesso." });
    }

    public class VerificarTelefoneRequest
    {
        public string PlaceId { get; set; } = null!;
        public string Telefone { get; set; } = null!;
    }

    public class ValidarCodigoRequest
    {
        public string PlaceId { get; set; } = null!;
        public string Code { get; set; } = null!;
    }
}