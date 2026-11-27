using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MesaAyuda.Infrastructure.Persistence;

/// <summary>
/// Fábrica en tiempo de diseño para permitir la generación de migraciones EF Core sin base de datos activa.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Crea una instancia de <see cref="AppDbContext"/> configurada con el proveedor Npgsql.
    /// </summary>
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=mesaayuda;Username=mesaayuda_user;Password=mesaayuda_pass_2026";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
