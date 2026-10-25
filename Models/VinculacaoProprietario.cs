using System.ComponentModel.DataAnnotations;

namespace projetoX.Models;

public class VinculacaoProprietario
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string EstabelecimentoPlaceId { get; set; } = null!;
    public string Status { get; set; } = "Pendente"; // "Pendente", "Aprovado", "Rejeitado"
    public DateTime DataSolicitacao { get; set; }
    public Cliente? Cliente { get; set; }
    public Estabelecimento? Estabelecimento { get; set; }
}