
namespace projetoX.DTOs;

public class EnderecoDTO
{
    public string Id { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? CEP { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? EmpresaNome { get; set; }
}