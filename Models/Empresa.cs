using System.ComponentModel.DataAnnotations;

namespace projetoX.Models;

public class Empresa
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string CNPJ { get; set; }
    public string Telefone { get; set; }
    public string? Instagram { get; set; }
    public string? Site { get; set; }
    public List<Endereco> Enderecos { get; set; } = new List<Endereco>();
    public List<Categoria> Categorias { get; set; } = new List<Categoria>();
    public bool Requerido {get; set;}
    public string? Nivel {get; set;}
}

