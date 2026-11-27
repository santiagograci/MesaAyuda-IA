using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Application.Services;
using MesaAyuda.Application.Validators;
using MesaAyuda.Domain.Entities;
using MesaAyuda.Domain.Enums;
using MesaAyuda.Domain.Exceptions;
using MesaAyuda.Domain.Interfaces;
using Xunit;

namespace MesaAyuda.UnitTests;

/// <summary>
/// Pruebas unitarias de reglas de dominio (<see cref="Caso"/>) y casos de uso (<see cref="CasoService"/>).
/// </summary>
public class CasoDomainAndServiceTests
{
    [Fact]
    public void CambiarEstado_DeResueltoACerrado_SinRespuestasPublicadas_DebeLanzarDomainException()
    {
        var caso = new Caso(
            "Error FAC-402 al anular factura DIAN",
            "El ERP bloquea la anulación directa de una factura electrónica ya aceptada.",
            PrioridadCaso.Alta,
            Guid.NewGuid());

        caso.CambiarEstado(EstadoCaso.EnProceso);
        caso.CambiarEstado(EstadoCaso.Resuelto);

        var exSinRespuestas = Assert.Throws<DomainException>(() => caso.CambiarEstado(EstadoCaso.Cerrado));
        Assert.Contains("respuesta publicada", exSinRespuestas.Message, StringComparison.OrdinalIgnoreCase);

        caso.AgregarSugerenciaIA("Borrador generado por IA pendiente de revisión.", "[]");
        Assert.Single(caso.Respuestas);
        Assert.False(caso.Respuestas.First().AprobadaPorAnalista);

        var exSoloBorradorIA = Assert.Throws<DomainException>(() => caso.CambiarEstado(EstadoCaso.Cerrado));
        Assert.Contains("respuesta publicada", exSoloBorradorIA.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CambiarEstado_FlujoCompletoValido_ConRespuestaAprobada_DebeCerrarCaso()
    {
        var caso = new Caso(
            "Cierre contable bloqueado por error CON-301",
            "El cierre de mes se detiene por comprobantes en estado Borrador y saldo en cuenta 999905.",
            PrioridadCaso.Critica,
            Guid.NewGuid());

        var sugerencia = caso.AgregarSugerenciaIA(
            "Revise comprobantes en estado Borrador o Descuadrado y limpie la cuenta puente 999905.",
            "[{\"documento\":\"02-cierre-contable-y-conciliacion.md\",\"seccion\":\"Diagnóstico\",\"fragmento\":\"CON-301\",\"puntaje\":0.82}]");

        caso.AprobarSugerenciaIA(
            sugerencia.Id,
            "Verificado: ejecute Auditoría de Comprobantes Pendientes y cierre cuenta 999905.",
            "Santiago Graciano - Analista Soporte");

        Assert.Equal(EstadoCaso.EnProceso, caso.Estado);
        Assert.True(sugerencia.AprobadaPorAnalista);

        caso.CambiarEstado(EstadoCaso.Resuelto);
        Assert.Equal(EstadoCaso.Resuelto, caso.Estado);

        caso.CambiarEstado(EstadoCaso.Cerrado);
        Assert.Equal(EstadoCaso.Cerrado, caso.Estado);
    }

    [Fact]
    public void CambiarEstado_SaltoInvalido_DeAbiertoACerrado_DebeLanzarDomainException()
    {
        var caso = new Caso(
            "Error INV-108 en bodega principal",
            "No permite facturar por existencia insuficiente en bodega principal.",
            PrioridadCaso.Media,
            Guid.NewGuid());

        var ex = Assert.Throws<DomainException>(() => caso.CambiarEstado(EstadoCaso.Cerrado));
        Assert.Contains("Transición de estado inválida", ex.Message);
    }

    [Fact]
    public void AgregarRespuestaHumana_CuandoEstaAbierto_DebeTransicionarAEnProceso()
    {
        var caso = new Caso(
            "Consulta sobre horas extras en nómina",
            "Requerimos confirmar el factor de hora extra nocturna y recargo dominical en liquidación.",
            PrioridadCaso.Baja,
            Guid.NewGuid());

        var respuesta = caso.AgregarRespuestaHumana(
            "La hora extra nocturna aplica recargo del 75% (factor 1.75) en Nómina > Procesos > Liquidación de Período.",
            "Santiago Graciano - Analista Soporte");

        Assert.Equal(EstadoCaso.EnProceso, caso.Estado);
        Assert.False(respuesta.EsGeneradaPorIA);
        Assert.True(respuesta.AprobadaPorAnalista);
        Assert.Single(caso.Respuestas);
    }

    [Fact]
    public async Task CasoService_ObtenerPorId_ClienteIntentandoVerCasoAjeno_DebeLanzarUnauthorizedAccessException()
    {
        var duenoId = Guid.NewGuid();
        var otroClienteId = Guid.NewGuid();

        var caso = new Caso(
            "Bloqueo de usuario con error SEC-423",
            "Cuenta bloqueada tras 5 intentos fallidos de acceso con 2FA.",
            PrioridadCaso.Media,
            duenoId);

        var casoRepo = new FakeCasoRepository(new[] { caso });
        var service = BuildCasoService(casoRepo, new FakeUsuarioRepository(), new FakeUnitOfWork(), new StubRagServiceClient());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ObtenerPorIdAsync(caso.Id, otroClienteId, nameof(RolUsuario.Cliente)));
    }

    [Fact]
    public async Task CasoService_GenerarSugerenciaIA_DebeGuardarBorradorNoAprobadoConFuentes()
    {
        var clienteId = Guid.NewGuid();
        var caso = new Caso(
            "Error FAC-402 al anular factura electrónica",
            "El cliente solicita anular una factura que ya aparece Aceptada por DIAN.",
            PrioridadCaso.Alta,
            clienteId);

        var casoRepo = new FakeCasoRepository(new[] { caso });
        var unitOfWork = new FakeUnitOfWork();
        var stubRag = new StubRagServiceClient();
        var service = BuildCasoService(casoRepo, new FakeUsuarioRepository(), unitOfWork, stubRag);

        var respuestaDto = await service.GenerarSugerenciaIAAsync(
            caso.Id,
            Guid.NewGuid(),
            nameof(RolUsuario.Analista),
            CancellationToken.None);

        Assert.True(respuestaDto.EsGeneradaPorIA);
        Assert.False(respuestaDto.AprobadaPorAnalista);
        Assert.Contains("NC-01", respuestaDto.Texto);
        Assert.Single(respuestaDto.FuentesCitadas);
        Assert.Equal("01-facturacion-electronica-y-anulaciones.md", respuestaDto.FuentesCitadas[0].Documento);

        var entidadGuardada = Assert.Single(caso.Respuestas);
        Assert.True(entidadGuardada.EsGeneradaPorIA);
        Assert.False(entidadGuardada.AprobadaPorAnalista);
        Assert.Contains("01-facturacion-electronica-y-anulaciones.md", entidadGuardada.FuentesCitadasJson);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    private static CasoService BuildCasoService(
        FakeCasoRepository casoRepo,
        FakeUsuarioRepository usuarioRepo,
        FakeUnitOfWork unitOfWork,
        IRagServiceClient ragClient)
    {
        return new CasoService(
            casoRepo,
            usuarioRepo,
            unitOfWork,
            ragClient,
            new CrearCasoRequestValidator(),
            new ActualizarCasoRequestValidator(),
            new CrearRespuestaRequestValidator(),
            new AprobarSugerenciaRequestValidator());
    }

    private sealed class FakeCasoRepository : ICasoRepository
    {
        private readonly List<Caso> _casos;

        public FakeCasoRepository(IEnumerable<Caso>? initial = null)
        {
            _casos = initial?.ToList() ?? new List<Caso>();
        }

        public Task<Caso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_casos.FirstOrDefault(c => c.Id == id));

        public Task<(IReadOnlyList<Caso> Items, int TotalCount)> GetPagedAsync(
            Guid? clienteId,
            EstadoCaso? estado,
            PrioridadCaso? prioridad,
            string? searchTerm,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<Caso> q = _casos;
            if (clienteId.HasValue) q = q.Where(c => c.ClienteId == clienteId.Value);
            if (estado.HasValue) q = q.Where(c => c.Estado == estado.Value);
            if (prioridad.HasValue) q = q.Where(c => c.Prioridad == prioridad.Value);
            var list = q.ToList();
            return Task.FromResult<(IReadOnlyList<Caso>, int)>((list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), list.Count));
        }

        public Task AddAsync(Caso caso, CancellationToken cancellationToken = default)
        {
            _casos.Add(caso);
            return Task.CompletedTask;
        }

        public Task AddRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Remove(Caso caso) => _casos.Remove(caso);
    }

    private sealed class FakeUsuarioRepository : IUsuarioRepository
    {
        private readonly List<Usuario> _usuarios = new();

        public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_usuarios.FirstOrDefault(u => u.Email == email));

        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_usuarios.FirstOrDefault(u => u.Id == id));

        public Task<IReadOnlyList<Usuario>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Usuario>>(_usuarios);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class StubRagServiceClient : IRagServiceClient
    {
        public Task<RagPreguntaResponse> ConsultarSugerenciaAsync(
            string pregunta,
            CancellationToken cancellationToken = default)
        {
            var response = new RagPreguntaResponse(
                Respuesta: "Emita una Nota Crédito electrónica NC-01 referenciando el CUFE original en Facturación > Notas > Nota Crédito Electrónica.",
                Fuentes: new List<FuenteCitadaDto>
                {
                    new(
                        Documento: "01-facturacion-electronica-y-anulaciones.md",
                        Seccion: "Reglas de Anulación Directa vs. Nota Crédito",
                        Fragmento: "Una factura con estado Aceptada por DIAN nunca puede anularse directamente (FAC-402).",
                        Puntaje: 0.8424)
                },
                TokensUsados: 245,
                Anclado: true,
                ProveedorLlm: "grounded-mock");

            return Task.FromResult(response);
        }
    }
}
