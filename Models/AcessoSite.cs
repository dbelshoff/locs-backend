
namespace projetoX.Models;

public class AcessoSite
{
    public int Id { get; set; }

    public DateTime DataHoraUtc { get; set; } = DateTime.UtcNow;

    public string Pagina { get; set; } = string.Empty;

    public string? Cidade { get; set; }

    public string? Estado { get; set; }

    public string? Pais { get; set; }
}
