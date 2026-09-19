using System.Net;
using System.Text;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Firmantes;
using FirmaElectronica.Infrastructure.Legalario;
using FirmaElectronica.Infrastructure.Quiter;
namespace FirmaElectronica.Tests;
public class TransporteLegalarioTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VistaUsaLigaDelDetalleOEndpointUrlSinDescargarDestino(bool directa)
    {
        using var transporte = new Transporte((r, _) =>
        {
            Assert.Equal("api.legalario.com", r.RequestUri!.Host);
            var detalle = r.RequestUri.AbsolutePath == "/v2/documents/d";
            if (!detalle) Assert.Contains("format=URL", r.RequestUri.Query);
            return Task.FromResult(Respuesta(detalle && !directa ? "{\"data\":{\"id\":\"d\"}}" : "{\"data\":{\"document\":\"https://storage.example.com/document.pdf?firma=prueba\"}}"));
        });
        using var http = new HttpClient(transporte);
        Assert.Equal("https://storage.example.com/document.pdf?firma=prueba", await Crear(http).ObtenerUrlDocumentoAsync("d", "token", default));
        Assert.Equal(directa ? 1 : 2, transporte.Envios);
    }
    [Fact]
    public async Task ConsultaPaginaCodificaBusquedaYLeeMetadatos()
    {
        using var transporte = new Transporte((r, _) =>
        {
            Assert.Contains("search=VIN%20%26%20nombre", r.RequestUri!.Query);
            Assert.Contains("page=2", r.RequestUri.Query);
            return Task.FromResult(Respuesta("{\"success\":true,\"data\":{\"data\":[{\"id\":\"d\"}],\"meta\":{\"last_page\":3,\"total\":25}}}"));
        });
        using var http = new HttpClient(transporte);
        var pagina = await Crear(http).ConsultarPaginaAsync("plantilla", 2, 10, "VIN & nombre", "token", default);
        Assert.Equal(3, pagina.UltimaPagina); Assert.Equal(25, pagina.Total); Assert.Single(pagina.Documentos);
    }
    [Fact]
    public async Task DescargaNoAceptaHtmlComoPdf()
    {
        using var transporte = new Transporte((r, _) => Task.FromResult(Respuesta(r.RequestUri!.Query == "" ? "{\"data\":{\"id\":\"d\"}}" : "<html>Error</html>")));
        using var http = new HttpClient(transporte);
        Assert.Null(await Crear(http).DescargarPdfAsync("d", "token", default));
    }
    [Fact]
    public async Task DescargaAceptaBytesPdf()
    {
        using var transporte = new Transporte((r, _) => Task.FromResult(Respuesta(r.RequestUri!.Query == "" ? "{\"data\":{\"id\":\"d\"}}" : "%PDF-1.7 contenido")));
        using var http = new HttpClient(transporte);
        var pdf = await Crear(http).DescargarPdfAsync("d", "token", default);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf!));
        Assert.Equal(2, transporte.Envios);
    }
    [Fact]
    public async Task ConvocatoriaMantieneContratoSinReintentar()
    {
        using var transporte = new Transporte(async (r, ct) =>
        {
            Assert.Equal("/v2/signers", r.RequestUri!.AbsolutePath);
            using var json = JsonDocument.Parse(await r.Content!.ReadAsStringAsync(ct));
            var raiz = json.RootElement;
            Assert.False(raiz.TryGetProperty("workflow", out _));
            Assert.True(raiz.GetProperty("send_invite").GetBoolean());
            Assert.True(raiz.GetProperty("use_whatsapp").GetBoolean());
            Assert.Equal("REPRESENTANTE LEGAL", raiz.GetProperty("signers")[0].GetProperty("type").GetString());
            Assert.Equal("FIRMANTE", raiz.GetProperty("signers")[0].GetProperty("role").GetString());
            return Respuesta("{}", HttpStatusCode.ServiceUnavailable);
        });
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<OperacionLegalarioException>(() => Crear(http).ConvocarFirmantesAsync("d", [new("Ana", "ana@example.com", "5512345678", TipoFirmante.RepresentanteLegal)], "token", default));
        Assert.True(error.ResultadoIncierto); Assert.Equal(1, transporte.Envios);
    }
    [Fact]
    public async Task EstadoUsaConteosDeFirmantes()
    {
        using var transporte = new Transporte((_, _) => Task.FromResult(Respuesta("{\"success\":true,\"data\":[{\"status\":\"confirmed\"},{\"status\":\"pending\"}]}")));
        using var http = new HttpClient(transporte);
        var estado = await Crear(http).ConsultarFirmasAsync("d", "token", default);
        Assert.Equal(1, estado.Firmados); Assert.Equal(2, estado.Convocados);
    }
    [Fact]
    public async Task ReenvioSinConfirmacionEsIncierto()
    {
        using var transporte = new Transporte((_, _) => Task.FromResult(Respuesta("{}")));
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<OperacionLegalarioException>(() => Crear(http).ReenviarInvitacionAsync("f1", "token", default));
        Assert.True(error.ResultadoIncierto);
        Assert.Equal(1, transporte.Envios);
    }
    [Fact]
    public async Task ReenvioYEliminacionUsanMetodoYRutaEsperados()
    {
        var rutas = new List<string>();
        using var transporte = new Transporte((r, _) =>
        { rutas.Add($"{r.Method} {r.RequestUri!.AbsolutePath}"); return Task.FromResult(Respuesta("", HttpStatusCode.NoContent)); });
        using var http = new HttpClient(transporte);
        await Crear(http).ReenviarInvitacionAsync("f1", "token", default);
        await Crear(http).EliminarDocumentoAsync("d1", "token", default);
        Assert.Equal(new[] { "POST /v2/signers/f1/invite", "DELETE /v2/documents/d1" }, rutas);
    }
    [Fact]
    public void QuiterLimpiaTelefonoYNoAgregaArreglosVacios()
    {
        var contacto = ClienteQuiter.PrepararContacto(new("1", " ana@example.com ", "+52 (55) 1234-5678"));
        Assert.Equal("ana@example.com", contacto["email"]);
        using var cuerpo = JsonDocument.Parse(JsonSerializer.Serialize(contacto));
        Assert.Equal("5512345678", cuerpo.RootElement.GetProperty("phoneNumbers")[0].GetProperty("phoneNumber").GetString());
        var soloCorreo = ClienteQuiter.PrepararContacto(new("1", "ana@example.com", ""));
        Assert.False(soloCorreo.ContainsKey("phoneNumbers"));
        Assert.Empty(ClienteQuiter.PrepararContacto(new("1", " ", "")));
    }
    [Fact]
    public async Task QuiterSinContactoNoSolicitaToken()
    {
        using var transporte = new Transporte((_, _) => throw new InvalidOperationException());
        using var http = new HttpClient(transporte);
        await new ClienteQuiter(http, new()).ActualizarContactoClienteAsync(new("1", "", ""), default);
        Assert.Equal(0, transporte.Envios);
    }
    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task QuiterEnviaContactoComoLegacyYDetectaRechazo(HttpStatusCode estado)
    {
        using var transporte = new Transporte(async (r, ct) =>
        {
            Assert.Contains(r.Headers.Accept, h => h.MediaType == "application/json");
            if (r.RequestUri!.AbsolutePath.EndsWith("/oauth/token"))
            {
                Assert.Equal(HttpMethod.Post, r.Method);
                Assert.Contains("grant_type=authorization_code", await r.Content!.ReadAsStringAsync(ct));
                return Respuesta("{\"access_token\":\"token-prueba\"}");
            }
            Assert.Equal(HttpMethod.Put, r.Method);
            Assert.Equal("/qis/api/customers/v1/customers/99619", r.RequestUri.AbsolutePath);
            Assert.Equal("Bearer token-prueba", r.Headers.Authorization!.ToString());
            using var body = JsonDocument.Parse(await r.Content!.ReadAsStringAsync(ct));
            Assert.Equal("cliente@example.com", body.RootElement.GetProperty("email").GetString());
            Assert.Equal("5512345678", body.RootElement.GetProperty("phoneNumbers")[0].GetProperty("phoneNumber").GetString());
            Assert.Equal("5512345678", body.RootElement.GetProperty("mobilePhoneNumber")[0].GetProperty("phoneNumber").GetString());
            Assert.True(body.RootElement.GetProperty("validated").GetBoolean());
            foreach (var campo in new[] { "phoneNumbers", "mobilePhoneNumber" })
                Assert.Equal("ACTUALIZADO DESDE FIRMA DIGITAL", body.RootElement.GetProperty(campo)[0].GetProperty("observations").GetString());
            return new HttpResponseMessage(estado);
        });
        using var http = new HttpClient(transporte);
        var cliente = new ClienteQuiter(http, new() { ClientId = "prueba", ClientSecret = "prueba", Code = "prueba" });
        var actualizar = () => cliente.ActualizarContactoClienteAsync(new("99619", "cliente@example.com", "5512345678"), default);
        if (estado == HttpStatusCode.NoContent) await actualizar();
        else await Assert.ThrowsAsync<InvalidOperationException>(actualizar);
        Assert.Equal(2, transporte.Envios);
    }
    [Theory]
    [InlineData(404)]
    [InlineData(408)]
    [InlineData(409)]
    [InlineData(425)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task LecturasTemporalesPermitenEsperarRepositorio(int codigo)
    {
        using var transporte = new Transporte((_, _) => Task.FromResult(Respuesta("{}", (HttpStatusCode)codigo)));
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<OperacionLegalarioException>(() => Crear(http).ConsultarFirmasAsync("d", "token", default));
        Assert.True(error.Reintentable);
        Assert.False(error.ResultadoIncierto);
        Assert.Equal(codigo, error.EstadoHttp);
    }
    [Theory]
    [InlineData(200, true, false)]
    [InlineData(422, true, false)]
    [InlineData(401, false, false)]
    [InlineData(403, false, false)]
    [InlineData(408, false, true)]
    [InlineData(500, false, true)]
    [InlineData(503, false, true)]
    public async Task RepositorioEnPostDistingueRechazoDeResultadoIncierto(int codigo, bool temporal, bool incierto)
    {
        using var transporte = new Transporte((_, _) => Task.FromResult(Respuesta("{\"success\":false,\"message\":\"El archivo no fue encontrado en el repositorio\"}", (HttpStatusCode)codigo)));
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<OperacionLegalarioException>(() => Crear(http).ConvocarFirmantesAsync("d", [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)], "token", default));
        Assert.Equal(temporal, error.Reintentable);
        Assert.Equal(incierto, error.ResultadoIncierto);
        Assert.Equal(1, transporte.Envios);
    }
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(422)]
    public async Task LecturaConRechazoDefinitivoNoSeReintenta(int codigo)
    {
        using var transporte = new Transporte((_, _) => Task.FromResult(Respuesta("{}", (HttpStatusCode)codigo)));
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<OperacionLegalarioException>(() => Crear(http).ConsultarFirmasAsync("d", "token", default));
        Assert.False(error.Reintentable);
    }
    private static ClienteLegalario Crear(HttpClient http) => new(http, new() { BaseUrl = "https://api.legalario.com" });
    private static HttpResponseMessage Respuesta(string cuerpo, HttpStatusCode estado = HttpStatusCode.OK) => new(estado) { Content = new StringContent(cuerpo) };
    private sealed class Transporte(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int Envios { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Envios++; return responder(request, cancellationToken); }
    }
}
