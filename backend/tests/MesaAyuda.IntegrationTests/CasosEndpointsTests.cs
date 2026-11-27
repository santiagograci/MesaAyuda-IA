using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MesaAyuda.Application.Common;
using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace MesaAyuda.IntegrationTests;

/// <summary>
/// Fábrica personalizada de pruebas que reemplaza PostgreSQL por EF Core InMemory
/// y el cliente HTTP RAG por un stub determinista.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MesaAyudaTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")))
                .ToList();

            foreach (var descriptor in dbDescriptors)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IRagServiceClient>();
            services.AddSingleton<IRagServiceClient, DeterministicRagStubClient>();
        });
    }

    private sealed class DeterministicRagStubClient : IRagServiceClient
    {
        public Task<RagPreguntaResponse> ConsultarSugerenciaAsync(
            string pregunta,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagPreguntaResponse(
                Respuesta: "Según el manual oficial, ante el error FAC-402 debe emitir una Nota Crédito electrónica NC-01 referenciando el CUFE en Facturación > Notas > Nota Crédito Electrónica.",
                Fuentes: new List<FuenteCitadaDto>
                {
                    new(
                        Documento: "01-facturacion-electronica-y-anulaciones.md",
                        Seccion: "Reglas de Anulación Directa vs. Nota Crédito",
                        Fragmento: "Una factura en estado Aceptada por DIAN no admite anulación directa (FAC-402).",
                        Puntaje: 0.85)
                },
                TokensUsados: 190,
                Anclado: true,
                ProveedorLlm: "stub-integration"));
        }
    }
}

/// <summary>
/// Pruebas de integración HTTP end-to-end sobre los controladores de Auth y Casos.
/// </summary>
public class CasosEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;

    public CasosEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostLogin_ConCredencialesValidas_RetornaJwtYDatosDeRol()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("analista@ofima.com", "Ofima2026*"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
        Assert.Equal("analista@ofima.com", payload.Email);
        Assert.Equal("Analista", payload.Rol);
    }

    [Fact]
    public async Task FlujoCompletoCaso_CrearPedirSugerenciaIAYCerrar_ConRolesCorrectos()
    {
        using var client = _factory.CreateClient();

        var loginCliente = await LoginAsync(client, "cliente@empresa.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginCliente.Token);

        var createResponse = await client.PostAsJsonAsync(
            "/api/casos",
            new CrearCasoRequest(
                "Error FAC-402 al anular factura aceptada por DIAN",
                "Necesitamos revertir una factura validada con CUFE que arroja el código FAC-402.",
                "Alta"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var casoCreado = await createResponse.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(casoCreado);
        Assert.Equal("Abierto", casoCreado!.Estado);

        var aprobarComoCliente = await client.PutAsJsonAsync(
            $"/api/casos/{casoCreado.Id}/respuestas/{Guid.NewGuid()}/aprobar",
            new AprobarSugerenciaRequest("Intento no autorizado desde rol cliente."));
        Assert.Equal(HttpStatusCode.Forbidden, aprobarComoCliente.StatusCode);

        var loginAnalista = await LoginAsync(client, "analista@ofima.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginAnalista.Token);

        var sugResponse = await client.PostAsync($"/api/casos/{casoCreado.Id}/sugerencia", null);
        Assert.Equal(HttpStatusCode.OK, sugResponse.StatusCode);

        var sugDto = await sugResponse.Content.ReadFromJsonAsync<RespuestaDto>(JsonOptions);
        Assert.NotNull(sugDto);
        Assert.True(sugDto!.EsGeneradaPorIA);
        Assert.False(sugDto.AprobadaPorAnalista);
        Assert.Contains(sugDto.FuentesCitadas, f => f.Documento == "01-facturacion-electronica-y-anulaciones.md");

        var aprobarResponse = await client.PutAsJsonAsync(
            $"/api/casos/{casoCreado.Id}/respuestas/{sugDto.Id}/aprobar",
            new AprobarSugerenciaRequest("Aprobado por soporte: emita Nota Crédito NC-01 referenciando el CUFE original."));

        Assert.Equal(HttpStatusCode.OK, aprobarResponse.StatusCode);
        var aprobadaDto = await aprobarResponse.Content.ReadFromJsonAsync<RespuestaDto>(JsonOptions);
        Assert.NotNull(aprobadaDto);
        Assert.True(aprobadaDto!.AprobadaPorAnalista);

        var resolverResponse = await client.PatchAsJsonAsync(
            $"/api/casos/{casoCreado.Id}/estado",
            new CambiarEstadoRequest("Resuelto"));
        Assert.Equal(HttpStatusCode.OK, resolverResponse.StatusCode);

        var cerrarResponse = await client.PatchAsJsonAsync(
            $"/api/casos/{casoCreado.Id}/estado",
            new CambiarEstadoRequest("Cerrado"));
        Assert.Equal(HttpStatusCode.OK, cerrarResponse.StatusCode);

        var casoCerrado = await cerrarResponse.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(casoCerrado);
        Assert.Equal("Cerrado", casoCerrado!.Estado);
    }

    [Fact]
    public async Task GetCasos_ComoCliente_SoloRetornaSusPropiosCasos()
    {
        using var client = _factory.CreateClient();

        var loginCliente1 = await LoginAsync(client, "cliente@empresa.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginCliente1.Token);

        var tituloUnico = $"Caso privado cliente1 {Guid.NewGuid():N}";
        var createRes = await client.PostAsJsonAsync(
            "/api/casos",
            new CrearCasoRequest(
                tituloUnico,
                "Descripción suficientemente extensa para validar propiedad por cliente en el listado.",
                "Media"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var casoCliente1 = await createRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(casoCliente1);

        var loginCliente2 = await LoginAsync(client, "cliente2@empresa.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginCliente2.Token);

        var listRes = await client.GetAsync("/api/casos?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);

        var paged = await listRes.Content.ReadFromJsonAsync<PagedResult<CasoResumenDto>>(JsonOptions);
        Assert.NotNull(paged);
        Assert.DoesNotContain(paged!.Items, c => c.Id == casoCliente1!.Id);
        Assert.DoesNotContain(paged.Items, c => c.Titulo == tituloUnico);

        var detalleAjenoRes = await client.GetAsync($"/api/casos/{casoCliente1!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, detalleAjenoRes.StatusCode);
    }

    [Fact]
    public async Task PatchEstado_CerrarCasoSinRespuestas_Retorna400ProblemDetails()
    {
        using var client = _factory.CreateClient();

        var loginCliente = await LoginAsync(client, "cliente@empresa.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginCliente.Token);

        var createRes = await client.PostAsJsonAsync(
            "/api/casos",
            new CrearCasoRequest(
                "Caso sin respuestas para validar cierre",
                "Prueba de integración para verificar bloqueo de cierre cuando no hay respuesta publicada.",
                "Baja"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var caso = await createRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(caso);

        var loginAnalista = await LoginAsync(client, "analista@ofima.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginAnalista.Token);

        var enProcesoRes = await client.PatchAsJsonAsync(
            $"/api/casos/{caso!.Id}/estado",
            new CambiarEstadoRequest("EnProceso"));
        Assert.Equal(HttpStatusCode.OK, enProcesoRes.StatusCode);

        var resueltoRes = await client.PatchAsJsonAsync(
            $"/api/casos/{caso.Id}/estado",
            new CambiarEstadoRequest("Resuelto"));
        Assert.Equal(HttpStatusCode.OK, resueltoRes.StatusCode);

        var cerrarRes = await client.PatchAsJsonAsync(
            $"/api/casos/{caso.Id}/estado",
            new CambiarEstadoRequest("Cerrado"));
        Assert.Equal(HttpStatusCode.BadRequest, cerrarRes.StatusCode);

        var problem = await cerrarRes.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Contains("respuesta publicada", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SoftDelete_Papelera_RestaurarYRestaurarDemo_FuncionaConControlDeRoles()
    {
        using var client = _factory.CreateClient();

        var loginCliente = await LoginAsync(client, "cliente@empresa.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginCliente.Token);

        var createRes = await client.PostAsJsonAsync(
            "/api/casos",
            new CrearCasoRequest(
                $"Caso temporal papelera {Guid.NewGuid():N}",
                "Prueba de integración para validar borrado lógico, papelera y restauración.",
                "Media"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var caso = await createRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(caso);

        // Cliente no puede consultar papelera, restaurar ni restaurar demo
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/casos/papelera")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/casos/{caso!.Id}/restaurar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/casos/restaurar-demo", null)).StatusCode);

        // Analista envía el caso a papelera (soft delete)
        var loginAnalista = await LoginAsync(client, "analista@ofima.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginAnalista.Token);

        var deleteRes = await client.DeleteAsync($"/api/casos/{caso.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var getDeletedRes = await client.GetAsync($"/api/casos/{caso.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedRes.StatusCode);

        var papeleraRes = await client.GetAsync("/api/casos/papelera");
        Assert.Equal(HttpStatusCode.OK, papeleraRes.StatusCode);
        var papelera = await papeleraRes.Content.ReadFromJsonAsync<List<CasoEliminadoDto>>(JsonOptions);
        Assert.NotNull(papelera);
        Assert.Contains(papelera!, c => c.Id == caso.Id);

        // Analista restaura el caso desde la papelera
        var restaurarRes = await client.PostAsync($"/api/casos/{caso.Id}/restaurar", null);
        Assert.Equal(HttpStatusCode.OK, restaurarRes.StatusCode);
        var restaurado = await restaurarRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(restaurado);
        Assert.Equal(caso.Id, restaurado!.Id);

        var getRestauradoRes = await client.GetAsync($"/api/casos/{caso.Id}");
        Assert.Equal(HttpStatusCode.OK, getRestauradoRes.StatusCode);

        // Eliminar un caso demo y validar POST /api/casos/restaurar-demo sin duplicar existentes
        var listRes = await client.GetAsync("/api/casos?page=1&pageSize=20");
        var paged = await listRes.Content.ReadFromJsonAsync<PagedResult<CasoResumenDto>>(JsonOptions);
        var demoFac = paged!.Items.First(c => c.Titulo.Contains("FAC-402", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/casos/{demoFac.Id}")).StatusCode);

        var demoRestoreRes = await client.PostAsync("/api/casos/restaurar-demo", null);
        Assert.Equal(HttpStatusCode.OK, demoRestoreRes.StatusCode);
        var demoRestoreBody = await demoRestoreRes.Content.ReadFromJsonAsync<RestaurarDemoResponse>(JsonOptions);
        Assert.NotNull(demoRestoreBody);
        Assert.True(demoRestoreBody!.CasosCreados >= 1);

        var demoRestoreIdempotent = await client.PostAsync("/api/casos/restaurar-demo", null);
        Assert.Equal(HttpStatusCode.OK, demoRestoreIdempotent.StatusCode);
        var idempotentBody = await demoRestoreIdempotent.Content.ReadFromJsonAsync<RestaurarDemoResponse>(JsonOptions);
        Assert.Equal(0, idempotentBody!.CasosCreados);
    }

    [Fact]
    public async Task PatchPrioridad_ClienteElevaYAnalistaAjusta_ConValidacionesYAuditoria()
    {
        using var client = _factory.CreateClient();

        var loginCliente = await LoginAsync(client, "cliente@empresa.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginCliente.Token);

        var createRes = await client.PostAsJsonAsync(
            "/api/casos",
            new CrearCasoRequest(
                $"Escalamiento prioridad {Guid.NewGuid():N}",
                "Prueba de integración para validar elevación de prioridad y trazabilidad en historial.",
                "Media"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var caso = await createRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(caso);

        // Cliente puede elevar Media -> Critica con justificación válida
        var elevarRes = await client.PatchAsJsonAsync(
            $"/api/casos/{caso!.Id}/prioridad",
            new
            {
                nuevaPrioridad = "Critica",
                justificacion = "Impacta la emisión de facturas electrónicas de cierre diario."
            });
        Assert.Equal(HttpStatusCode.OK, elevarRes.StatusCode);
        var elevado = await elevarRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(elevado);
        Assert.Equal("Critica", elevado!.Prioridad);
        Assert.Contains(
            elevado.Respuestas,
            r => r.Texto.Contains("[Cambio de Prioridad: Media ➔ Critica]", StringComparison.Ordinal));

        // Cliente no puede reducir Critica -> Baja
        var degradarRes = await client.PatchAsJsonAsync(
            $"/api/casos/{caso.Id}/prioridad",
            new
            {
                nuevaPrioridad = "Baja",
                justificacion = "Intento no autorizado de bajar severidad desde rol Cliente."
            });
        Assert.Equal(HttpStatusCode.BadRequest, degradarRes.StatusCode);

        // Justificación menor a 10 caracteres retorna 400 BadRequest
        var cortaRes = await client.PatchAsJsonAsync(
            $"/api/casos/{caso.Id}/prioridad",
            new
            {
                nuevaPrioridad = "Alta",
                justificacion = "Muy corta"
            });
        Assert.Equal(HttpStatusCode.BadRequest, cortaRes.StatusCode);

        // Analista sí puede recalibrar Critica -> Media
        var loginAnalista = await LoginAsync(client, "analista@ofima.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginAnalista.Token);

        var ajustarRes = await client.PatchAsJsonAsync(
            $"/api/casos/{caso.Id}/prioridad",
            new
            {
                nuevaPrioridad = "Media",
                justificacion = "Se confirmó workaround operativo mientras se aplica el parche."
            });
        Assert.Equal(HttpStatusCode.OK, ajustarRes.StatusCode);
        var ajustado = await ajustarRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(ajustado);
        Assert.Equal("Media", ajustado!.Prioridad);
        Assert.Contains(
            ajustado.Respuestas,
            r => r.Texto.Contains("[Cambio de Prioridad: Critica ➔ Media]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetMetricas_YPostSugerenciaStream_RetornaKpisYSseCorrectamente()
    {
        using var client = _factory.CreateClient();

        var loginCliente = await LoginAsync(client, "cliente@empresa.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginCliente.Token);

        var metricasClienteRes = await client.GetAsync("/api/casos/metricas");
        Assert.Equal(HttpStatusCode.OK, metricasClienteRes.StatusCode);
        var metricasCliente = await metricasClienteRes.Content.ReadFromJsonAsync<MetricasMesaAyudaDto>(JsonOptions);
        Assert.NotNull(metricasCliente);
        Assert.True(metricasCliente!.TotalCasos >= 1);

        var createRes = await client.PostAsJsonAsync(
            "/api/casos",
            new CrearCasoRequest(
                $"Streaming SSE {Guid.NewGuid():N}",
                "Consulta para probar generación en tiempo real por Server-Sent Events y consumo FinOps.",
                "Critica"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var caso = await createRes.Content.ReadFromJsonAsync<CasoDetalleDto>(JsonOptions);
        Assert.NotNull(caso);

        // Cliente no está autorizado al endpoint stream
        var forbiddenStream = await client.PostAsync($"/api/casos/{caso!.Id}/sugerencia/stream", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenStream.StatusCode);

        var loginAnalista = await LoginAsync(client, "analista@ofima.com", "Ofima2026*");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginAnalista.Token);

        var streamRes = await client.PostAsync($"/api/casos/{caso.Id}/sugerencia/stream", null);
        Assert.Equal(HttpStatusCode.OK, streamRes.StatusCode);
        Assert.Equal("text/event-stream", streamRes.Content.Headers.ContentType?.MediaType);

        var sseBody = await streamRes.Content.ReadAsStringAsync();
        Assert.Contains("event: sources", sseBody);
        Assert.Contains("event: token", sseBody);
        Assert.Contains("event: done", sseBody);

        var metricasAnalistaRes = await client.GetAsync("/api/casos/metricas");
        Assert.Equal(HttpStatusCode.OK, metricasAnalistaRes.StatusCode);
        var metricasAnalista = await metricasAnalistaRes.Content.ReadFromJsonAsync<MetricasMesaAyudaDto>(JsonOptions);
        Assert.NotNull(metricasAnalista);
        Assert.True(metricasAnalista!.SugerenciasIaGeneradas >= 1);
        Assert.True(metricasAnalista.TokensTotalesConsumidos >= 190);
        Assert.True(metricasAnalista.CasosCriticosActivos >= 1);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }
}
