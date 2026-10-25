using System.ComponentModel.DataAnnotations;

namespace projetoX.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public List<Empresa> Empresas { get; set; } = new List<Empresa>();
}
