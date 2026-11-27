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

    [Fact]
    public async Task EliminarYRestaurarCaso_ComoAnalista_AplicaSoftDeleteYPermiteRestauracion()
    {
        var caso = new Caso(
            "Error FAC-402 al anular factura aceptada",
            "El cliente solicita orientación para emitir nota crédito electrónica.",
            PrioridadCaso.Alta,
            Guid.NewGuid());
        var repo = new FakeCasoRepository(new[] { caso });
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, new FakeUsuarioRepository(), uow, new StubRagServiceClient());

        await service.EliminarCasoAsync(caso.Id, "Analista");

        Assert.True(caso.Eliminado);
        Assert.NotNull(caso.FechaEliminacion);
        Assert.Null(await repo.GetByIdAsync(caso.Id));

        var papelera = await service.ListarPapeleraAsync("Analista");
        Assert.Single(papelera, p => p.Id == caso.Id);

        Assert.Throws<DomainException>(() => caso.CambiarEstado(EstadoCaso.EnProceso));

        var restaurado = await service.RestaurarCasoAsync(caso.Id, "Analista");
        Assert.Equal(caso.Id, restaurado.Id);
        Assert.False(caso.Eliminado);
        Assert.Null(caso.FechaEliminacion);
        Assert.NotNull(await repo.GetByIdAsync(caso.Id));
        Assert.Equal(2, uow.SaveChangesCount);
    }

    [Fact]
    public async Task RestaurarDatosDemo_ComoAnalista_RecreaSoloFaltantesSinDuplicar()
    {
        var cliente1 = new Usuario("Carlos Mendoza", "cliente@empresa.com", "hash", RolUsuario.Cliente);
        var cliente2 = new Usuario("Laura Gómez", "cliente2@empresa.com", "hash", RolUsuario.Cliente);
        var analista = new Usuario("Santiago Graciano", "analista@ofima.com", "hash", RolUsuario.Analista);

        var repo = new FakeCasoRepository();
        var userRepo = new FakeUsuarioRepository(new[] { cliente1, cliente2, analista });
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, userRepo, uow, new StubRagServiceClient());

        var primera = await service.RestaurarDatosDemoAsync("Analista");
        Assert.Equal(3, primera.CasosCreados);

        var segunda = await service.RestaurarDatosDemoAsync("Analista");
        Assert.Equal(0, segunda.CasosCreados);
    }

    [Fact]
    public async Task CambiarPrioridad_ComoCliente_ElevaConAuditoria_YRechazaDegradacion()
    {
        var cliente = new Usuario("Carlos Mendoza", "cliente@empresa.com", "hash", RolUsuario.Cliente);
        var caso = new Caso(
            "Error en reporte de cartera",
            "Los saldos por tercero presentan diferencia temporal en pantalla.",
            PrioridadCaso.Media,
            cliente.Id);
        var repo = new FakeCasoRepository(new[] { caso });
        var userRepo = new FakeUsuarioRepository(new[] { cliente });
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, userRepo, uow, new StubRagServiceClient());

        var actualizado = await service.CambiarPrioridadAsync(
            caso.Id,
            new CambiarPrioridadRequest(
                PrioridadCaso.Critica,
                "El módulo de facturación quedó bloqueado para toda la operación."),
            cliente.Id,
            "Cliente",
            cliente.Nombre);

        Assert.Equal("Critica", actualizado.Prioridad);
        Assert.Equal("Abierto", actualizado.Estado);
        Assert.NotNull(caso.FechaActualizacion);
        var audit = Assert.Single(actualizado.Respuestas);
        Assert.Contains("[Cambio de Prioridad: Media ➔ Critica]", audit.Texto);
        Assert.Contains("El módulo de facturación quedó bloqueado", audit.Texto);
        Assert.False(audit.EsGeneradaPorIA);
        Assert.True(audit.AprobadaPorAnalista);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CambiarPrioridadAsync(
                caso.Id,
                new CambiarPrioridadRequest(PrioridadCaso.Baja, "Justificación suficientemente larga para intentar bajar."),
                cliente.Id,
                "Cliente"));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CambiarPrioridadAsync(
                caso.Id,
                new CambiarPrioridadRequest(PrioridadCaso.Alta, "Corta"),
                cliente.Id,
                "Cliente"));
    }

    [Fact]
    public async Task CambiarPrioridad_ComoAnalista_PermiteAjustarEnCualquierDireccion()
    {
        var caso = new Caso(
            "Incidencia menor de visualización",
            "Etiqueta desalineada en consulta de inventario.",
            PrioridadCaso.Alta,
            Guid.NewGuid());
        var repo = new FakeCasoRepository(new[] { caso });
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, new FakeUsuarioRepository(), uow, new StubRagServiceClient());

        var ajustado = await service.CambiarPrioridadAsync(
            caso.Id,
            new CambiarPrioridadRequest(
                PrioridadCaso.Baja,
                "Tras evaluación técnica no bloquea operación ni facturación."),
            Guid.NewGuid(),
            "Analista",
            "Santiago Graciano - Analista Soporte");

        Assert.Equal("Baja", ajustado.Prioridad);
        var audit = Assert.Single(ajustado.Respuestas);
        Assert.Contains("[Cambio de Prioridad: Alta ➔ Baja]", audit.Texto);
    }

    [Fact]
    public async Task ListarCasos_ConPrioridadYBusqueda_FiltraCorrectamentePorTituloYCliente()
    {
        var c1 = new Usuario("Carlos Mendoza - Contabilidad", "c1@empresa.com", "h", RolUsuario.Cliente);
        var c2 = new Usuario("Laura Gómez - Tesorería", "c2@empresa.com", "h", RolUsuario.Cliente);

        var casoA = new Caso("Error CON-301 cierre contable", "Saldo pendiente cuenta 999905 en octubre.", PrioridadCaso.Critica, c1.Id);
        var casoB = new Caso("Consulta catálogo inventarios", "Duda general sobre creación de bodega.", PrioridadCaso.Baja, c2.Id);
        typeof(Caso).GetProperty(nameof(Caso.Cliente))!.SetValue(casoA, c1);
        typeof(Caso).GetProperty(nameof(Caso.Cliente))!.SetValue(casoB, c2);

        var repo = new FakeCasoRepository(new[] { casoA, casoB });
        var service = BuildCasoService(repo, new FakeUsuarioRepository(new[] { c1, c2 }), new FakeUnitOfWork(), new StubRagServiceClient());

        var porCodigo = await service.ListarCasosAsync(c1.Id, "Analista", null, 1, 10, "Critica", "con-301");
        var item1 = Assert.Single(porCodigo.Items);
        Assert.Equal(casoA.Id, item1.Id);

        var porCliente = await service.ListarCasosAsync(c1.Id, "Analista", null, 1, 10, "Baja", "Laura");
        var item2 = Assert.Single(porCliente.Items);
        Assert.Equal(casoB.Id, item2.Id);
    }

    [Fact]
    public async Task Sla_Deflexion_YCsat_CumplenReglasDeNegocioYMetricas()
    {
        var cliente = new Usuario("Carlos Mendoza", "cliente@empresa.com", "hash", RolUsuario.Cliente);
        var caso = new Caso(
            "Bloqueo en facturación electrónica DIAN",
            "El módulo presenta error FAC-402 al anular comprobantes.",
            PrioridadCaso.Critica,
            cliente.Id);

        Assert.Equal(caso.FechaCreacion.AddHours(2), caso.CalcularFechaLimiteSla());
        Assert.Equal("EnTiempo", caso.EvaluarEstadoSla(caso.FechaCreacion.AddMinutes(60)));
        Assert.Equal("PorVencer", caso.EvaluarEstadoSla(caso.FechaCreacion.AddMinutes(95)));
        Assert.Equal("Vencido", caso.EvaluarEstadoSla(caso.FechaCreacion.AddHours(3)));

        var repo = new FakeCasoRepository(new[] { caso });
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, new FakeUsuarioRepository(new[] { cliente }), uow, new StubRagServiceClient());

        // Deflexión preventiva devuelve guía con hasta 2 fuentes sin guardar casos en repo
        var deflexion = await service.ConsultarDeflexionAsync(
            new DeflexionRequest("Error FAC-402", "¿Cómo anular una factura electrónica aceptada por DIAN?"));
        Assert.True(deflexion.TieneSugerencia);
        Assert.True(deflexion.Fuentes.Count <= 2);
        Assert.Equal(0, uow.SaveChangesCount);

        // No permite CSAT mientras está Abierto
        await Assert.ThrowsAsync<DomainException>(() =>
            service.RegistrarCsatAsync(caso.Id, new RegistrarCsatRequest(5, "Excelente"), cliente.Id, "Cliente"));

        caso.AgregarRespuestaHumana("Se emitió Nota Crédito NC-01 correctamente.", "Analista");
        caso.CambiarEstado(EstadoCaso.Resuelto);
        Assert.Equal("Cumplido", caso.EvaluarEstadoSla());

        var conCsat = await service.RegistrarCsatAsync(
            caso.Id,
            new RegistrarCsatRequest(5, "Solución rápida y precisa."),
            cliente.Id,
            "Cliente");

        Assert.Equal(5, conCsat.CalificacionCsat);
        Assert.Equal("Solución rápida y precisa.", conCsat.ComentarioCsat);
        Assert.Equal("Cumplido", conCsat.EstadoSla);

        // No permite duplicar CSAT ni que Analista califique
        await Assert.ThrowsAsync<DomainException>(() =>
            service.RegistrarCsatAsync(caso.Id, new RegistrarCsatRequest(4, "Cambio"), cliente.Id, "Cliente"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RegistrarCsatAsync(caso.Id, new RegistrarCsatRequest(5, null), cliente.Id, "Analista"));

        var metricas = await service.ObtenerMetricasAsync(cliente.Id, "Analista");
        Assert.Equal(1, metricas.TotalEncuestasCsat);
        Assert.Equal(5.0, metricas.PromedioCsat);
    }

    [Fact]
    public async Task ReabrirCaso_DesdeResueltoOCerrado_TransicionaAEnProcesoRecalculaSlaYRegistraAuditoria()
    {
        var cliente = new Usuario("Carlos Mendoza", "cliente@empresa.com", "hash", RolUsuario.Cliente);
        var otroClienteId = Guid.NewGuid();
        var caso = new Caso(
            "Error CON-301 en cierre contable",
            "El módulo contable detiene el cierre de mes por cuenta puente.",
            PrioridadCaso.Critica,
            cliente.Id);

        caso.AgregarRespuestaHumana("Se concilió la cuenta puente 999905.", "Santiago Graciano");
        caso.CambiarEstado(EstadoCaso.Resuelto);
        caso.CambiarEstado(EstadoCaso.Cerrado);

        // Invariante original de CambiarEstado sobre Cerrado debe mantenerse intacta
        Assert.Throws<DomainException>(() => caso.CambiarEstado(EstadoCaso.EnProceso));

        var repo = new FakeCasoRepository(new[] { caso });
        var userRepo = new FakeUsuarioRepository(new[] { cliente });
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, userRepo, uow, new StubRagServiceClient());

        // Otro cliente no autorizado -> UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ReabrirCasoAsync(
                caso.Id,
                new ReabrirCasoRequest("Intento no autorizado de reapertura."),
                otroClienteId,
                "Cliente"));

        // Motivo menor a 10 caracteres -> DomainException
        await Assert.ThrowsAsync<DomainException>(() =>
            service.ReabrirCasoAsync(
                caso.Id,
                new ReabrirCasoRequest("Muy corto"),
                cliente.Id,
                "Cliente"));

        // Dueño reabre el caso Cerrado -> retorna a EnProceso con auditoría
        var reabierto = await service.ReabrirCasoAsync(
            caso.Id,
            new ReabrirCasoRequest("El error CON-301 volvió a aparecer al ejecutar el cierre definitivo."),
            cliente.Id,
            "Cliente",
            cliente.Nombre);

        Assert.Equal("EnProceso", reabierto.Estado);
        Assert.NotEqual("Cumplido", reabierto.EstadoSla);
        Assert.Contains(reabierto.Respuestas, r =>
            r.Texto.Contains("🔄 [Caso Reabierto: El error CON-301 volvió a aparecer", StringComparison.Ordinal));
        Assert.Equal(1, uow.SaveChangesCount);

        // También permite reabrir desde Resuelto
        caso.CambiarEstado(EstadoCaso.Resuelto);
        var reabiertoDesdeResuelto = await service.ReabrirCasoAsync(
            caso.Id,
            new ReabrirCasoRequest("Pendiente validar un comprobante adicional de tesorería."),
            Guid.NewGuid(),
            "Analista",
            "Santiago Graciano");
        Assert.Equal("EnProceso", reabiertoDesdeResuelto.Estado);
    }

    [Fact]
    public async Task RegistrarDeflexionEvitada_PersisteEnRepositorioYSeReflejaEnMetricas()
    {
        var repo = new FakeCasoRepository();
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, new FakeUsuarioRepository(), uow, new StubRagServiceClient());

        var res1 = await service.RegistrarDeflexionEvitadaAsync();
        var res2 = await service.RegistrarDeflexionEvitadaAsync();

        Assert.Equal(1, res1.TotalDeflexionesEvitadas);
        Assert.Equal(2, res2.TotalDeflexionesEvitadas);

        var metricas = await service.ObtenerMetricasAsync(Guid.NewGuid(), "Analista");
        Assert.Equal(2, metricas.TotalDeflexionesEvitadas);
        Assert.Equal(2, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ImagenAdjunta_EnCasoYRespuesta_ValidaFormatoYTamanoMax2MB()
    {
        const string pngValido = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
        var cliente = new Usuario("Carlos Mendoza", "cliente@empresa.com", "hash", RolUsuario.Cliente);
        var repo = new FakeCasoRepository();
        var userRepo = new FakeUsuarioRepository(new[] { cliente });
        var uow = new FakeUnitOfWork();
        var service = BuildCasoService(repo, userRepo, uow, new StubRagServiceClient());

        var creado = await service.CrearCasoAsync(
            new CrearCasoRequest(
                "Error FAC-402 con captura adjunta",
                "Adjuntamos evidencia de pantalla del error FAC-402 en módulo de facturación.",
                "Alta",
                pngValido),
            cliente.Id);

        Assert.Equal(pngValido, creado.ImagenAdjuntaBase64);

        var resp = await service.AgregarRespuestaHumanaAsync(
            creado.Id,
            new CrearRespuestaRequest("Adjunto captura del paso a paso en Nota Crédito NC-01.", pngValido),
            Guid.NewGuid(),
            "Santiago Graciano",
            "Analista");

        Assert.Equal(pngValido, resp.ImagenAdjuntaBase64);

        // Rechaza MIME no permitido y tamaño > 2 MB
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.CrearCasoAsync(
                new CrearCasoRequest(
                    "Caso con archivo inválido",
                    "Descripción válida de prueba para rechazar archivo adjunto inválido.",
                    "Media",
                    "data:application/pdf;base64,JVBERi0xLjQ="),
                cliente.Id));

        var demasiadoGrande = "data:image/png;base64," + Convert.ToBase64String(new byte[(2 * 1024 * 1024) + 10]);
        Assert.Throws<DomainException>(() =>
            new Caso("Caso excedido", "Descripción suficientemente extensa para el caso.", PrioridadCaso.Baja, cliente.Id, demasiadoGrande));
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
        private int _deflexiones;

        public FakeCasoRepository(IEnumerable<Caso>? initial = null)
        {
            _casos = initial?.ToList() ?? new List<Caso>();
        }

        public Task<Caso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_casos.FirstOrDefault(c => c.Id == id && !c.Eliminado));

        public Task<Caso?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_casos.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<Caso>> ListarEliminadosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Caso>>(
                _casos.Where(c => c.Eliminado).OrderByDescending(c => c.FechaEliminacion).ToList());

        public Task<bool> ExistsActiveByTitleAsync(string titulo, CancellationToken cancellationToken = default) =>
            Task.FromResult(_casos.Any(c => !c.Eliminado && string.Equals(c.Titulo, titulo.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task<(IReadOnlyList<Caso> Items, int TotalCount)> GetPagedAsync(
            Guid? clienteId,
            EstadoCaso? estado,
            PrioridadCaso? prioridad,
            string? searchTerm,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<Caso> q = _casos.Where(c => !c.Eliminado);
            if (clienteId.HasValue) q = q.Where(c => c.ClienteId == clienteId.Value);
            if (estado.HasValue) q = q.Where(c => c.Estado == estado.Value);
            if (prioridad.HasValue) q = q.Where(c => c.Prioridad == prioridad.Value);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLowerInvariant();
                q = q.Where(c =>
                    c.Titulo.ToLower().Contains(term) ||
                    c.Descripcion.ToLower().Contains(term) ||
                    (c.Cliente != null && c.Cliente.Nombre.ToLower().Contains(term)));
            }
            var list = q.ToList();
            return Task.FromResult<(IReadOnlyList<Caso>, int)>((list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), list.Count));
        }

        public Task AddAsync(Caso caso, CancellationToken cancellationToken = default)
        {
            _casos.Add(caso);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Caso>> ListarActivosConRespuestasAsync(
            Guid? clienteId,
            CancellationToken cancellationToken = default)
        {
            var query = _casos.Where(c => !c.Eliminado);
            if (clienteId.HasValue)
                query = query.Where(c => c.ClienteId == clienteId.Value);
            return Task.FromResult<IReadOnlyList<Caso>>(query.ToList());
        }

        public Task AddRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Remove(Caso caso) => _casos.Remove(caso);

        public Task<int> IncrementarDeflexionAsync(CancellationToken cancellationToken = default)
        {
            _deflexiones++;
            return Task.FromResult(_deflexiones);
        }

        public Task<int> ObtenerDeflexionesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_deflexiones);
    }

    private sealed class FakeUsuarioRepository : IUsuarioRepository
    {
        private readonly List<Usuario> _usuarios;

        public FakeUsuarioRepository(IEnumerable<Usuario>? initial = null)
        {
            _usuarios = initial?.ToList() ?? new List<Usuario>();
        }

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

        public Task<DocumentosRagResponse> ListarDocumentosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentosRagResponse(
                new List<DocumentoIndexadoDto> { new("01-facturacion-electronica-y-anulaciones.md", 14) },
                1,
                14));

        public Task<IngestaArchivoResponse> IngestarArchivoAsync(
            string fileName,
            byte[] content,
            string contentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new IngestaArchivoResponse(fileName, 3, 2, 17, "ok"));
    }
}
