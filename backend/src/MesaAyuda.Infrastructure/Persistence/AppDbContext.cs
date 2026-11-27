using MesaAyuda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Infrastructure.Persistence;

/// <summary>
/// Contexto de Entity Framework Core para la persistencia de usuarios, casos y respuestas en PostgreSQL.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>Conjunto de usuarios registrados en la plataforma.</summary>
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    /// <summary>Conjunto de casos de soporte empresarial.</summary>
    public DbSet<Caso> Casos => Set<Caso>();

    /// <summary>Conjunto de respuestas humanas y sugerencias IA asociadas a casos.</summary>
    public DbSet<Respuesta> Respuestas => Set<Respuesta>();

    /// <summary>
    /// Inicializa el contexto con las opciones configuradas por inyección de dependencias.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Configura el mapeo relacional, índices únicos, conversiones de enums y relaciones en cascada.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(builder =>
        {
            builder.ToTable("usuarios");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Nombre)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(180);

            builder.HasIndex(u => u.Email)
                .IsUnique();

            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(u => u.Rol)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(u => u.FechaCreacion)
                .IsRequired();
        });

        modelBuilder.Entity<Caso>(builder =>
        {
            builder.ToTable("casos");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Titulo)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.Descripcion)
                .IsRequired()
                .HasMaxLength(4000);

            builder.Property(c => c.Prioridad)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(c => c.Estado)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(c => c.FechaCreacion)
                .IsRequired();

            builder.Property(c => c.FechaActualizacion);

            builder.Property(c => c.Eliminado)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(c => c.FechaEliminacion);

            builder.HasOne(c => c.Cliente)
                .WithMany()
                .HasForeignKey(c => c.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(c => c.Respuestas)
                .WithOne()
                .HasForeignKey(r => r.CasoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(c => c.Respuestas)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(c => c.Estado);
            builder.HasIndex(c => c.ClienteId);
            builder.HasIndex(c => c.Eliminado);
        });

        modelBuilder.Entity<Respuesta>(builder =>
        {
            builder.ToTable("respuestas");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Texto)
                .IsRequired()
                .HasMaxLength(8000);

            builder.Property(r => r.EsGeneradaPorIA)
                .IsRequired();

            builder.Property(r => r.AprobadaPorAnalista)
                .IsRequired();

            builder.Property(r => r.AutorNombre)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(r => r.FuentesCitadasJson)
                .IsRequired()
                .HasDefaultValue("[]");

            builder.Property(r => r.FechaCreacion)
                .IsRequired();

            builder.HasIndex(r => r.CasoId);
        });
    }
}
