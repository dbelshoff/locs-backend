using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projetoX.Data;
using projetoX.Models;
using projetoX.DTOs;

namespace projetoX.Controllers;

[Route("api/clientes")]
//[Authorize]
[ApiController]
public class ClienteController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClienteController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult CriarCliente([FromBody] Cliente cliente)
    {
        // Verifica se já existe um cliente com o mesmo e-mail
        bool emailJaExiste = _context.Clientes.Any(c => c.Email == cliente.Email);
        if (emailJaExiste)
        {
            return BadRequest(new { mensagem = "O e-mail informado já está cadastrado." });
        }

        // Criptografa a senha e salva o cliente
        cliente.SenhaHash = BCrypt.Net.BCrypt.HashPassword(cliente.SenhaHash);
        _context.Clientes.Add(cliente);
        _context.SaveChanges();

        return CreatedAtAction(nameof(ObterCliente), new { id = cliente.Id }, cliente);
    }

    [HttpGet("{id}")]
    public IActionResult ObterCliente(int id)
    {
        var cliente = _context.Clientes.Find(id);
        if (cliente == null)
            return NotFound();

        var clienteDto = new ClienteDTO
        {
            Id = cliente.Id,
            Nome = cliente.Nome,
            Email = cliente.Email,
            Telefone = cliente.Telefone,
        };

        return Ok(clienteDto);
    }
}
