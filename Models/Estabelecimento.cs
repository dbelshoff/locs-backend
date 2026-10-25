using System.ComponentModel.DataAnnotations;

namespace projetoX.Models;

public class Estabelecimento
{
    [Key]
    public string PlaceId { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string FormattedAddress { get; set; } = null!;
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Telefone { get; set; }
    public bool? AbertoAgora { get; set; }
    public string? Foto { get; set; }
    public string? Types { get; set; }
    public double Rating { get; set; }
    public DateTime UltimaAtualizacao { get; set; }
    public int? ProprietarioId { get; set; } // Chave estrangeira para o cliente proprietário
    public Cliente? Proprietario { get; set; } // Relação com Cliente
    public string? Cep { get; set; }
}