using System.ComponentModel.DataAnnotations;

namespace projetoX.Models;

public class Lead
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; }
    public int? ClienteId { get; set; }
    public string IP { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
}
