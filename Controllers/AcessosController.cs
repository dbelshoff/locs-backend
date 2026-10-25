
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projetoX.Data;
using projetoX.Models;

namespace projetoX.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AcessosController : ControllerBase
{
    private readonly AppDbContext _context;

    public AcessosController(AppDbContext context)
    {
        _context = context;
    }

    public class RegistrarAcessoRequest
    {
        public string Pagina { get; set; } = string.Empty;
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? Pais { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarAcessoRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pagina))
        {
            return BadRequest(new { mensagem = "A página é obrigatória." });
        }

        var acesso = new AcessoSite
        {
            DataHoraUtc = DateTime.UtcNow,
            Pagina = request.Pagina,
            Cidade = request.Cidade,
            Estado = request.Estado,
            Pais = request.Pais
        };

        _context.AcessosSite.Add(acesso);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { sucesso = true });
    }

    [HttpGet("quantidade")]
    public async Task<IActionResult> Quantidade(
        CancellationToken cancellationToken)
    {
        var total = await _context.AcessosSite
            .CountAsync(cancellationToken);

        return Ok(new { total });
    }
}
