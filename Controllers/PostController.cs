using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projetoX.Data;
using projetoX.DTOs;
using projetoX.Models;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.Annotations;

namespace projetoX.Controllers;

[Route("api/posts")]
[ApiController]
[Authorize]
public class PostController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly string _postsPath;

    public PostController(AppDbContext context)
    {
        _context = context;
        _postsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "posts");
        if (!Directory.Exists(_postsPath))
        {
            Directory.CreateDirectory(_postsPath);
        }
    }

    [HttpPost("upload-imagem")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> UploadImagem([FromForm] IFormFile imagem, [FromForm] string placeId)
    {
        var clienteId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (clienteId == 0) return Unauthorized("Usuário não autenticado.");

        var estabelecimento = await _context.Estabelecimentos
            .FirstOrDefaultAsync(e => e.PlaceId == placeId && e.ProprietarioId == clienteId);
        if (estabelecimento == null)
            return Forbid("Você não é o proprietário deste estabelecimento.");

        if (imagem == null || imagem.Length == 0)
            return BadRequest("Nenhuma imagem enviada.");

        if (imagem.Length > 5 * 1024 * 1024) // Limite de 5MB
            return BadRequest("Imagem muito grande. Máximo 5MB.");

        var extensao = Path.GetExtension(imagem.FileName).ToLower();
        if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(extensao))
            return BadRequest("Apenas imagens JPEG ou PNG são permitidas.");

        var nomeArquivo = $"{placeId}_{Guid.NewGuid().ToString("N")}{extensao}";
        var caminhoArquivo = Path.Combine(_postsPath, nomeArquivo);

        using (var stream = new FileStream(caminhoArquivo, FileMode.Create))
        {
            await imagem.CopyToAsync(stream);
        }

        return Ok(new { imagemUrl = nomeArquivo });
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Cria um novo post", Description = "Cria um post para o estabelecimento vinculado")]
    public async Task<IActionResult> CriarPost([FromBody] PostDTO dto)
    {
        var clienteId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (clienteId == 0) return Unauthorized("Usuário não autenticado.");

        var estabelecimento = await _context.Estabelecimentos
            .FirstOrDefaultAsync(e => e.PlaceId == dto.PlaceId && e.ProprietarioId == clienteId);
        if (estabelecimento == null)
            return Forbid("Você não é o proprietário deste estabelecimento.");

        if (dto.ExpiresAt <= DateTime.UtcNow)
            return BadRequest("A data de expiração deve ser futura.");

        var post = new Post
        {
            PlaceId = dto.PlaceId,
            Conteudo = dto.Conteudo,
            ImagemUrl = dto.ImagemUrl,
            ExpiresAt = dto.ExpiresAt
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Post criado com sucesso.", postId = post.Id });
    }

    [HttpGet("{placeId}")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Lista posts válidos de um estabelecimento", Description = "Retorna posts não expirados")]
    public async Task<IActionResult> GetPosts(string placeId)
    {
        var agora = DateTime.UtcNow;
        var posts = await _context.Posts
            .Where(p => p.PlaceId == placeId && p.IsActive && p.ExpiresAt > agora)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PostDTO
            {
                Id = p.Id,
                PlaceId = p.PlaceId,
                Conteudo = p.Conteudo,
                //ImagemUrl = p.ImagemUrl != null ? $"/posts/{p.ImagemUrl}" : null,
                ImagemUrl = p.ImagemUrl != null ? $"{p.ImagemUrl}" : null,
                CreatedAt = p.CreatedAt,
                ExpiresAt = p.ExpiresAt
            })
            .ToListAsync();

        return Ok(posts);
    }

    [HttpGet("imagem")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Obtém uma imagem de um post", Description = "Retorna a imagem pelo nome do arquivo")]
    public IActionResult ObterImagem([FromQuery] string imagemNome)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagemNome))
                return BadRequest("Nome da imagem é obrigatório.");

            var imagemPath = Path.Combine(_postsPath, imagemNome);
            if (!System.IO.File.Exists(imagemPath))
                return NotFound("Imagem não encontrada.");

            var imageBytes = System.IO.File.ReadAllBytes(imagemPath);
            var extensao = Path.GetExtension(imagemNome).ToLower();
            var contentType = extensao == ".png" ? "image/png" : "image/jpeg";
            return File(imageBytes, contentType);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro ao obter imagem: {ex.Message}");
        }
    }
}