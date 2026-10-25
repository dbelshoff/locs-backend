namespace projetoX.DTOs;

public class EmpresaDTO
{
    public string Id { get; set; }
    public string Nome { get; set; }
    public string? CNPJ { get; set; }
    public string? Telefone { get; set; }
    public string? Instagram { get; set; }
    public string? Site { get; set; }
    public bool Requerido { get; set; }
    public string? Nivel { get; set; }
    public List<CategoriaDTO> Categorias { get; set; } = new List<CategoriaDTO>();
    public List<EnderecoDTO> Enderecos { get; set; } = new List<EnderecoDTO>();
    public double? Rating { get; set; }
}