using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projetoX.Data;
using projetoX.Models;

[ApiController]
[Route("api/[controller]")]
public class LeadController : ControllerBase
{
    private readonly AppDbContext _context;

    public LeadController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/lead
    [HttpPost]
    public async Task<IActionResult> CreateLead([FromBody] Lead lead)
    {
        if (lead == null)
            return BadRequest("Dados inválidos.");

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLeadById), new { id = lead.Id }, lead);
    }

    // GET: api/lead
    [HttpGet]
    public async Task<IActionResult> GetLeads()
    {
        var leads = await _context.Leads.Include(l => l.Empresa).ToListAsync();
        return Ok(leads);
    }

    // GET: api/lead/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetLeadById(int id)
    {
        var lead = await _context
            .Leads.Include(l => l.Empresa)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lead == null)
            return NotFound("Lead não encontrado.");

        return Ok(lead);
    }
}
