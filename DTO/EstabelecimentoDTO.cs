using System.Collections.Generic;

namespace projetoX.DTOs;

public class EstabelecimentoDTO
{
    public string PlaceId { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string FormattedAddress { get; set; } = null!;
    public string Logradouro { get; set; } = null!;
    public string Numero { get; set; } = null!;
    public string Bairro { get; set; } = null!;
    public string Cidade { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Telefone { get; set; } = null!;
    public bool? AbertoAgora { get; set; }
    public string FotoUrl { get; set; } = null!; // URL da foto (ex.: /fotos/nome_arquivo.jpg)
    public List<string> Types { get; set; } = new List<string>();
    public string TypesString { get; set; } = null!; // Adicionado para armazenar Types bruto
    public double Rating { get; set; }
    public string? Cep { get; set; }
}