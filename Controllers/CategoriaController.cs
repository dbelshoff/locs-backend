using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projetoX.Data;
using projetoX.DTOs;
using projetoX.Models;

namespace projetoX.Controllers;

[Route("api/categorias")]
[ApiController]
public class CategoriaController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriaController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult ListarCategorias()
    {
        var categorias = _context
            .Categorias.Select(c => new CategoriaDTO { Id = c.Id, Nome = c.Nome })
            .ToList();

        return Ok(categorias);
    }
}
