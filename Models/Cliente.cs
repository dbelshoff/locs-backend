using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace projetoX.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string SenhaHash { get; set; } = null!;
    public string Telefone { get; set; } = null!;
    public List<Estabelecimento> Estabelecimentos { get; set; } = new List<Estabelecimento>();
}