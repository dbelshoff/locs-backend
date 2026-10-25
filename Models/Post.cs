using System;

namespace projetoX.Models;

public class Post
{
    public int Id { get; set; }
    public string PlaceId { get; set; } = null!; // Chave estrangeira para Estabelecimento
    public string Conteudo { get; set; } = null!; // Texto do post
    public string? ImagemUrl { get; set; } // Nome do arquivo da imagem (ex: placeId_guid.jpg)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } // Data de expiração
    public bool IsActive { get; set; } = true; // Para desativar manualmente

    public Estabelecimento Estabelecimento { get; set; } = null!;
}