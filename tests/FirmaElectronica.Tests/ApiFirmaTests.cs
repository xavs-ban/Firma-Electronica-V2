using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Firmantes;
using FirmaElectronica.Domain.Plantillas;
using FirmaElectronica.Domain.Usuarios;
using FirmaElectronica.Infrastructure.Intentos;
using FirmaElectronica.Infrastructure.Legalario;
using FirmaElectronica.Web.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace FirmaElectronica.Tests;
public class ApiFirmaTests
{
    [Fact]
    public async Task SubrutaConservaRecursosCsrfYLogin()
    {
        await using var aplicacion = new Aplicacion("/firma-digital");
        using var cliente = aplicacion.CreateClient();
        var pagina = await cliente.GetStringAsync("/firma-digital/");
        Assert.Contains("content=\"/firma-digital/\"", pagina);
        Assert.Matches("/firma-digital/js/site(?:\\.[a-zA-Z0-9]+)?\\.js", pagina);
        using var csrf = JsonDocument.Parse(await cliente.GetStringAsync("/firma-digital/api/sesion/csrf"));
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        var acceso = await cliente.PostAsJsonAsync("/firma-digital/api/sesion", new Acceso("prueba", "clave"));
        Assert.Equal(HttpStatusCode.OK, acceso.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/firma-digital/api/agencias")).StatusCode);
    }
    [Fact]
    public async Task RutasPrivadasExigenSesion()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/agencias")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/documentos?agencia=306")).StatusCode);
    }
    [Fact]
    public async Task LoginRechazaSolicitudSinCsrf()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/sesion", new Acceso("prueba", "clave"))).StatusCode);
        Assert.Equal(0, aplicacion.Autorizaciones);
    }
    [Fact]
    public async Task LoginPermisosYSalidaFuncionanSinExponerToken()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente);
        var sesion = await cliente.GetStringAsync("/api/sesion");
        Assert.DoesNotContain("token-interno", sesion);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/agencias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/referencias/sin-permiso")).StatusCode);
        await Csrf(cliente);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.DeleteAsync("/api/sesion")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/agencias")).StatusCode);
    }
    [Fact]
    public async Task FlujoHttpPreparaGeneraConsultaYDescargaSinServiciosReales()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente); await Csrf(cliente);
        var entrada = new GenerarEntrada(new("ref", "306", "CON", new(2026, 9, 8), false), new("1001", new(2026, 9, 8), null), Guid.NewGuid());
        var preparacion = await cliente.PostAsJsonAsync("/api/documentos/preparar", entrada);
        Assert.Equal(HttpStatusCode.OK, preparacion.StatusCode);
        var creado = await cliente.PostAsJsonAsync("/api/documentos", entrada);
        Assert.Equal(HttpStatusCode.OK, creado.StatusCode);
        Assert.Contains("doc-1", await creado.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/documentos", entrada)).StatusCode);
        Assert.Equal(1, aplicacion.Creaciones);
        var convocatoria = await cliente.GetAsync("/api/documentos/doc-1/preparar-firmantes?agencia=306");
        Assert.Equal(HttpStatusCode.OK, convocatoria.StatusCode);
        using var contactos = JsonDocument.Parse(await convocatoria.Content.ReadAsStringAsync());
        Assert.Equal("ref", contactos.RootElement.GetProperty("referencia").GetString());
        Assert.NotEmpty(contactos.RootElement.GetProperty("firmantes").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/documentos?agencia=306")).StatusCode);
        var pdf = await cliente.GetAsync("/api/documentos/doc-1/pdf?agencia=306");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/documentos/doc-1/firmas?agencia=306")).StatusCode);
    }
    [Fact]
    public async Task DocumentoSinAsociacionNoInventaReferencia()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.GetAsync("/api/documentos/doc-1/preparar-firmantes?agencia=306")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/documentos/doc-1/preparar-firmantes?agencia=474")).StatusCode);
    }
    [Fact]
    public async Task FolioConLetrasNoGeneraDocumento()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente); await Csrf(cliente);
        var entrada = new GenerarEntrada(new("1234", "306", "CON", new(2026, 9, 8), false), new("F1", new(2026, 9, 8), null));
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/documentos", entrada)).StatusCode);
        Assert.Equal(0, aplicacion.Creaciones);
    }
    [Fact]
    public async Task TrabajoDevuelveAceptadoYSeConsultaHastaCompletar()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente); await Csrf(cliente);
        var entrada = new GenerarEntrada(new("ref", "306", "CON", new(2026, 9, 8), false), new("1001", new(2026, 9, 8), null), Guid.NewGuid());
        var respuesta = await cliente.PostAsJsonAsync("/api/documentos/trabajos", entrada);
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.NotNull(respuesta.Headers.Location);
        EstadoTrabajo? estado = null;
        for (var i = 0; i < 100; i++)
        {
            estado = await cliente.GetFromJsonAsync<EstadoTrabajo>(respuesta.Headers.Location);
            if (estado!.Estado is not ("EnCola" or "Procesando")) break;
            await Task.Delay(20);
        }
        Assert.Equal("Completado", estado!.Estado);
        Assert.Equal("doc-1", estado.Documento!.LegalarioDocumentId);
        Assert.Equal(1, aplicacion.Creaciones);
        var intento = await cliente.GetAsync("/api/intentos/ref?agencia=306&plantilla=" + CatalogoPlantillas.Contado["306"]);
        Assert.Equal(HttpStatusCode.OK, intento.StatusCode);
        Assert.Contains("Confirmado", await intento.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("sin-permiso", HttpStatusCode.Forbidden, "No tienes acceso")]
    public async Task ReferenciaDeOtraAgenciaExplicaElProblemaSinEntregarExpediente(string referencia, HttpStatusCode estado, string mensaje)
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente);
        var respuesta = await cliente.GetAsync($"/api/referencias/{referencia}?agencia=306");
        Assert.Equal(estado, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains(mensaje, json.RootElement.GetProperty("mensaje").GetString());
        Assert.False(json.RootElement.TryGetProperty("nombre_completo", out _));
    }
    [Theory]
    [InlineData("/api/referencias/otra-agencia")]
    [InlineData("/api/referencias/otra-agencia?agencia=306")]
    public async Task ReferenciaDetectaAgenciaSinDependerDelSelector(string ruta)
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente);
        var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("527", json.RootElement.GetProperty("dealer").GetString());
    }
    [Fact]
    public async Task NuevaGeneracionExigeSesionCsrfAgenciaPlantillaEIdentificadorAnterior()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        var ruta = "/api/intentos/ref/nueva-generacion?agencia=306&plantilla=" + CatalogoPlantillas.Contado["306"];
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync(ruta, new NuevaGeneracionEntrada("doc-1"))).StatusCode);
        await Entrar(cliente); await Csrf(cliente);
        var entrada = new GenerarEntrada(new("ref", "306", "CON", new(2026, 9, 8), false), new("1001", new(2026, 9, 8), null), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/documentos", entrada)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/documentos", entrada with { OperacionId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(2, aplicacion.Creaciones);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(ruta, new NuevaGeneracionEntrada("incorrecto"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync(ruta.Replace("agencia=306", "agencia=474"), new NuevaGeneracionEntrada("doc-1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/intentos/ref/nueva-generacion?agencia=306&plantilla=ajena", new NuevaGeneracionEntrada("doc-1"))).StatusCode);
        cliente.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(ruta, new NuevaGeneracionEntrada("doc-1"))).StatusCode);
        await Csrf(cliente);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync(ruta, new NuevaGeneracionEntrada("doc-1"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/documentos", entrada with { OperacionId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(3, aplicacion.Creaciones);
    }
    [Fact]
    public async Task DocumentoPendienteExponeReintentoSeguroAntesDeConvocar()
    {
        await using var aplicacion = new Aplicacion { DocumentoPendiente = true };
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente); await Csrf(cliente);
        var respuesta = await cliente.PostAsJsonAsync("/api/documentos/doc-1/convocar", new ConvocarEntrada("ref", "306", []));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        using var error = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.True(error.RootElement.GetProperty("reintentable").GetBoolean());
        Assert.False(error.RootElement.GetProperty("resultadoIncierto").GetBoolean());
        Assert.Equal(0, aplicacion.Creaciones);
    }
    [Fact]
    public async Task ConsultaYConvocatoriaUsanSesionAunqueElTokenAdicionalFalleCon500()
    {
        await using var aplicacion = new Aplicacion();
        using var cliente = aplicacion.CreateClient();
        await Entrar(cliente); await Csrf(cliente);
        Assert.Equal(new[] { "customers" }, aplicacion.Scopes);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/documentos/doc-1/firmas?agencia=306")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/documentos/doc-1/firmas?agencia=306")).StatusCode);
        Assert.Equal(1, aplicacion.Autorizaciones);
        Firmante[] firmantes = [
            new("Representante", "rep@example.com", "5512345678", TipoFirmante.RepresentanteLegal),
            new("Gerente", "gerente@example.com", "5512345678", TipoFirmante.GerenteDeVentas),
            new("Asesor", "asesor@example.com", "5512345678", TipoFirmante.Apv),
            new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)
        ];
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/documentos/doc-1/convocar", new ConvocarEntrada("ref", "306", firmantes))).StatusCode);
        Assert.Equal(1, aplicacion.Autorizaciones);
        Assert.Equal(new[] { "customers" }, aplicacion.Scopes);
        var sesion = await cliente.GetStringAsync("/api/sesion");
        Assert.DoesNotContain("secreto-falso", sesion);
        Assert.DoesNotContain("cliente-falso", sesion);
    }
    [Fact]
    public async Task ConfiguracionTemporalAntiguaNoSustituyeLaCuentaIndividual()
    {
        await using var aplicacion = new Aplicacion { AccesoTemporal = true, UsuarioTemporal = "cuenta-compartida" };
        using var cliente = aplicacion.CreateClient();
        await Csrf(cliente);
        var respuesta = await cliente.PostAsJsonAsync("/api/sesion", new Acceso("prueba", "clave-entregas"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Single(aplicacion.Logins);
        Assert.Contains("email=prueba", aplicacion.Logins[0]);
        Assert.Contains("password=clave-entregas", aplicacion.Logins[0]);
        Assert.DoesNotContain("cuenta-compartida", aplicacion.Logins[0]);
        var sesion = await cliente.GetStringAsync("/api/sesion");
        Assert.Contains("prueba", sesion);
        Assert.DoesNotContain("clave-compartida", sesion);
        var pagina = await cliente.GetStringAsync("/");
        Assert.DoesNotContain("data-acceso-temporal", pagina);
        Assert.DoesNotContain("clave-compartida", pagina);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/agencias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/referencias/sin-permiso")).StatusCode);
    }
    [Fact]
    public async Task ConfiguracionTemporalIncompletaNoImpideLoginIndividual()
    {
        await using var aplicacion = new Aplicacion { AccesoTemporal = true, ContrasenaTemporal = "" };
        using var cliente = aplicacion.CreateClient();
        await Csrf(cliente);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/sesion", new Acceso("prueba", "clave"))).StatusCode);
        Assert.Single(aplicacion.Logins);
    }
    [Fact]
    public async Task SesionCompartidaAnteriorSeInvalidaAunqueSigaConfigurada()
    {
        await using var aplicacion = new Aplicacion { AccesoTemporal = true };
        using var cliente = aplicacion.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var opciones = aplicacion.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        var identidad = new System.Security.Claims.ClaimsIdentity([
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "prueba"), new("agencias", "306"), new("acceso_temporal", "prueba")], "Cookies");
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new(identidad), new() { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, "Cookies");
        cliente.DefaultRequestHeaders.Add("Cookie", "FirmaDigital.Auth=" + opciones.TicketDataFormat.Protect(ticket));
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/agencias")).StatusCode);
    }
    private static async Task Entrar(HttpClient cliente)
    {
        await Csrf(cliente);
        var respuesta = await cliente.PostAsJsonAsync("/api/sesion", new Acceso("prueba", "clave"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
    private static async Task Csrf(HttpClient cliente)
    {
        using var json = JsonDocument.Parse(await cliente.GetStringAsync("/api/sesion/csrf"));
        cliente.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.RootElement.GetProperty("token").GetString());
    }
    private sealed class Aplicacion(string? rutaBase = null) : WebApplicationFactory<Program>
    {
        private readonly string carpeta = Path.Combine(Path.GetTempPath(), "firma-api-" + Guid.NewGuid().ToString("N"));
        public int Creaciones, Autorizaciones;
        public bool DocumentoPendiente;
        public bool AccesoTemporal;
        public string UsuarioTemporal = "prueba";
        public string ContrasenaTemporal = "clave-compartida-prueba";
        public List<string> Logins { get; } = [];
        public List<string> Scopes { get; } = [];
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Legalario:BaseUrl"] = "https://api.legalario.com",
                ["Hosting:PathBase"] = rutaBase,
                ["AccesoTemporal:Activo"] = AccesoTemporal.ToString(),
                ["AccesoTemporal:Usuario"] = UsuarioTemporal,
                ["AccesoTemporal:Contrasena"] = ContrasenaTemporal
            }));
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<IConsultaPerfilUsuario>(); servicios.AddSingleton<IConsultaPerfilUsuario, Autenticador>();
                servicios.RemoveAll<IReferenciaDataProvider>(); servicios.AddSingleton<IReferenciaDataProvider, Referencias>();
                servicios.RemoveAll<IRegistroIntentos>(); servicios.AddSingleton<IRegistroIntentos>(new RegistroIntentosArchivo(carpeta));
                servicios.AddHttpClient<AutorizacionLegalario>().ConfigurePrimaryHttpMessageHandler(() => new Transporte(Responder));
                servicios.AddHttpClient<ILegalarioClient, ClienteLegalario>().ConfigurePrimaryHttpMessageHandler(() => new Transporte(Responder));
            });
        }
        private HttpResponseMessage Responder(HttpRequestMessage solicitud)
        {
            var ruta = solicitud.RequestUri!.AbsolutePath;
            var plantilla = CatalogoPlantillas.Contado["306"];
            if (ruta == "/auth/login") { Logins.Add(solicitud.Content!.ReadAsStringAsync().GetAwaiter().GetResult()); return Json(new { success = true, data = new { client_id = "cliente-falso", client_secret = "secreto-falso" } }); }
            if (ruta == "/auth/token")
            {
                Interlocked.Increment(ref Autorizaciones);
                var cuerpo = solicitud.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var scope = Uri.UnescapeDataString(cuerpo.Split('&').Single(p => p.StartsWith("scope="))[6..]);
                Scopes.Add(scope);
                if (scope != "customers") return new(HttpStatusCode.InternalServerError) { Content = JsonContent.Create(new { message = "Token adicional rechazado" }) };
                return Json(new { data = new { access_token = "token-interno" } });
            }
            Assert.Equal("token-interno", solicitud.Headers.Authorization?.Parameter);
            if (ruta == "/v2/documents" && solicitud.Method == HttpMethod.Post)
            { Interlocked.Increment(ref Creaciones); return Json(new { data = new { id = "doc-1" } }); }
            if (ruta == "/v2/documents") return Json(new { success = true, data = new { data = Array.Empty<object>(), meta = new { last_page = 1, total = 0 } } });
            if (ruta == "/v2/documents/doc-1" && DocumentoPendiente) return new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { success = false }) };
            if (ruta == "/v2/documents/doc-1") return Json(new { data = new { id = "doc-1", template_id = plantilla, name = "Documentacion_ANA_VIN" } });
            if (ruta == "/v2/documents/download") return new(HttpStatusCode.OK) { Content = new ByteArrayContent("%PDF-1.7 simulado"u8.ToArray()) };
            if (ruta == "/v2/signers") return Json(new { success = true, data = Array.Empty<object>() });
            throw new InvalidOperationException("Solicitud no prevista en la prueba.");
        }
        private static HttpResponseMessage Json(object valor) => new(HttpStatusCode.OK) { Content = JsonContent.Create(valor) };
        public override async ValueTask DisposeAsync()
        { await base.DisposeAsync(); if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); }
    }
    private sealed class Autenticador : IConsultaPerfilUsuario
    {
        public Task<UsuarioFirma?> ConsultarAsync(string usuario, CancellationToken cancellationToken) =>
            Task.FromResult<UsuarioFirma?>(usuario == "prueba" ? new("prueba", "Prueba", "usuario", "306,527") : null);
    }
    private sealed class Referencias : IReferenciaDataProvider
    {
        public Task<IReadOnlyDictionary<string, object?>> ObtenerDatosReferenciaAsync(string referencia, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?> { ["dealer"] = referencia == "otra-agencia" ? "527" : referencia == "sin-permiso" ? "474" : "306", ["nombre_completo"] = "ANA", ["vin"] = "VIN", ["tipo_de_venta"] = "1" });
    }
    private sealed class Transporte(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }
}
