using Microsoft.EntityFrameworkCore;
using projetoX.Models;

namespace projetoX.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Empresa> Empresas { get; set; }
    public DbSet<Endereco> Enderecos { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Estabelecimento> Estabelecimentos { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }

    public DbSet<Post> Posts { get; set; }
    public DbSet<AcessoSite> AcessosSite { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração da relação Endereco-Empresa
        modelBuilder.Entity<Endereco>()
            .HasOne(e => e.Empresa)
            .WithMany(emp => emp.Enderecos)
            .HasForeignKey(e => e.EmpresaId);

        // Configuração da chave primária do Estabelecimento
        modelBuilder.Entity<Estabelecimento>()
            .HasKey(e => e.PlaceId);

        // Configuração da relação Estabelecimento-Cliente
        modelBuilder.Entity<Estabelecimento>()
            .HasOne(e => e.Proprietario)
            .WithMany(c => c.Estabelecimentos)
            .HasForeignKey(e => e.ProprietarioId)
            .IsRequired(false);

        // Configuração da relação VerificationCode
        modelBuilder.Entity<VerificationCode>()
            .HasOne(v => v.Cliente)
            .WithMany()
            .HasForeignKey(v => v.ClienteId);

        modelBuilder.Entity<VerificationCode>()
            .HasOne(v => v.Estabelecimento)
            .WithMany()
            .HasForeignKey(v => v.EstabelecimentoPlaceId);
    }
}