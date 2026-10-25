using System.ComponentModel.DataAnnotations;

namespace projetoX.Models;

public class VerificationCode
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string EstabelecimentoPlaceId { get; set; } = null!;
    public string Code { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public Cliente? Cliente { get; set; }
    public Estabelecimento? Estabelecimento { get; set; }
}