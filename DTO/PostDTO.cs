using System;

namespace projetoX.DTOs;

public class PostDTO
{
    public int Id { get; set; }

    public string PlaceId { get; set; }
    public string Conteudo { get; set; } = null!;
    public string? ImagemUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}