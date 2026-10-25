using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using projetoX.Data;

namespace projetoX.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Carrega as variáveis de ambiente explicitamente
        var environmentVariables = Environment.GetEnvironmentVariables();
        Console.WriteLine("Variáveis de ambiente disponíveis:");
        foreach (var key in environmentVariables.Keys)
        {
            Console.WriteLine($"{key}: {environmentVariables[key]}");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionStringFromConfig = configuration.GetConnectionString("DefaultConnection");
        var connectionStringFromEnv = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Console.WriteLine($"Connection String from appsettings.json: {connectionStringFromConfig}");
        Console.WriteLine($"Connection String from Environment: {connectionStringFromEnv}");

        var connectionString = connectionStringFromEnv ?? connectionStringFromConfig;

        if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("${CONNECTION_STRING}"))
        {
            throw new InvalidOperationException("Connection string não configurada. Verifique o appsettings.json ou a variável de ambiente CONNECTION_STRING.");
        }

        Console.WriteLine($"Final Connection String: {connectionString}");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySQL(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}