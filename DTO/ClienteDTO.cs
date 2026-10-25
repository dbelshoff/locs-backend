using System.Collections.Generic;

namespace projetoX.DTOs;

public class ClienteDTO
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Telefone { get; set; } = null!;
    public List<EstabelecimentoDTO> Estabelecimentos { get; set; } = new List<EstabelecimentoDTO>();
}