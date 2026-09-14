using System.Net;
using System.Text;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Infrastructure.Legalario;

namespace FirmaElectronica.Tests;

public class CreadorDocumentoLegalarioTests
{
    private static DocumentoParaCrear Documento => new("123", "Documentación_PRUEBA_VIN", "plantilla",
        new Dictionary<int, string> { [2] = "", [1] = "José" });

    [Fact]
    public async Task ConservaEstructuraOrdenYTokenPorSolicitud()
    {
        var tokens = new List<string?>();
        using var transporte = new Transporte(async (solicitud, ct) =>
        {
            Assert.Equal(HttpMethod.Post, solicitud.Method);
            Assert.Equal("https://api.legalario.com/v2/documents", solicitud.RequestUri!.AbsoluteUri);
            tokens.Add(solicitud.Headers.Authorization?.Parameter);
            Assert.Equal("Bearer", solicitud.Headers.Authorization?.Scheme);
            using var json = JsonDocument.Parse(await solicitud.Content!.ReadAsStringAsync(ct));
            var raiz = json.RootElement;
            Assert.Equal("template", raiz.GetProperty("type").GetString());
            Assert.Equal(Documento.Nombre, raiz.GetProperty("name").GetString());
            Assert.Equal("plantilla", raiz.GetProperty("template_id").GetString());
            var secuencia = raiz.GetProperty("sequence");
            Assert.Equal(2, secuencia.GetArrayLength());
            Assert.Equal(1, secuencia[0].GetArrayLength());
            Assert.Equal(1, secuencia[0][0].GetProperty("key").GetInt32());
            Assert.Equal("José", secuencia[0][0].GetProperty("value").GetString());
            Assert.Equal("", secuencia[1][0].GetProperty("value").GetString());
            return Respuesta(HttpStatusCode.Created, "{\"data\":{\"id\":\"documento-1\"}}");
        });
        using var http = new HttpClient(transporte);
        var creador = Crear(http);
        var resultado = await creador.CrearDocumentoAsync(Documento, "sesion-a", default);
        await creador.CrearDocumentoAsync(Documento, "sesion-b", default);
        Assert.Equal("documento-1", resultado.LegalarioDocumentId);
        Assert.Equal("123", resultado.Referencia);
        Assert.Equal(new[] { "sesion-a", "sesion-b" }, tokens);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData(401, false)]
    [InlineData(422, false)]
    [InlineData(408, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(302, true)]
    public async Task ClasificaErroresSinReintentarNiExponerRespuesta(int codigo, bool incierto)
    {
        using var transporte = new Transporte((_, _) => Task.FromResult(Respuesta((HttpStatusCode)codigo, "dato-privado")));
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<CreacionDocumentoException>(() => Crear(http).CrearDocumentoAsync(Documento, "secreto", default));
        Assert.Equal(incierto, error.ResultadoIncierto);
        Assert.Equal((HttpStatusCode)codigo, error.EstadoHttp);
        Assert.DoesNotContain("dato-privado", error.ToString());
        Assert.DoesNotContain("secreto", error.ToString());
        Assert.Equal(1, transporte.Envios);
    }

    [Theory]
    [InlineData("<html>Error</html>")]
    [InlineData("{}")]
    [InlineData("{\"data\":{\"id\":\" \"}}")]
    [InlineData("{\"data\":{\"id\":7}}")]
    [InlineData("{\"success\":false,\"data\":{\"id\":\"x\"}}")]
    public async Task RespuestaIncompletaRequiereConciliacion(string cuerpo)
    {
        using var transporte = new Transporte((_, _) => Task.FromResult(Respuesta(HttpStatusCode.OK, cuerpo)));
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<CreacionDocumentoException>(() => Crear(http).CrearDocumentoAsync(Documento, "token", default));
        Assert.True(error.ResultadoIncierto);
        Assert.Equal(1, transporte.Envios);
    }

    [Fact]
    public async Task CorteDeRedNoRepiteCreacion()
    {
        using var transporte = new Transporte((_, _) => throw new HttpRequestException("detalle privado"));
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<CreacionDocumentoException>(() => Crear(http).CrearDocumentoAsync(Documento, "token", default));
        Assert.True(error.ResultadoIncierto);
        Assert.DoesNotContain("detalle privado", error.ToString());
        Assert.Equal(1, transporte.Envios);
    }

    [Fact]
    public async Task TiempoLimiteInterrumpeUnSoloEnvio()
    {
        using var transporte = new Transporte(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Respuesta(HttpStatusCode.OK, "{}");
        });
        using var http = new HttpClient(transporte);
        var error = await Assert.ThrowsAsync<CreacionDocumentoException>(() => Crear(http, 1).CrearDocumentoAsync(Documento, "token", default));
        Assert.True(error.ResultadoIncierto);
        Assert.Equal(1, transporte.Envios);
    }

    [Fact]
    public async Task CancelacionPreviaNoEnvia()
    {
        using var transporte = new Transporte((_, _) => throw new InvalidOperationException());
        using var http = new HttpClient(transporte);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Crear(http).CrearDocumentoAsync(Documento, "token", new CancellationToken(true)));
        Assert.Equal(0, transporte.Envios);
    }

    [Fact]
    public async Task PosicionesIncompletasNoLleganALegalario()
    {
        using var transporte = new Transporte((_, _) => throw new InvalidOperationException());
        using var http = new HttpClient(transporte);
        var documento = Documento with { Variables = new Dictionary<int, string> { [2] = "valor" } };
        await Assert.ThrowsAsync<ArgumentException>(() => Crear(http).CrearDocumentoAsync(documento, "token", default));
        Assert.Equal(0, transporte.Envios);
    }

    private static CreadorDocumentoLegalario Crear(HttpClient http, int segundos = 120) =>
        new(http, new LegalarioOptions { BaseUrl = "https://api.legalario.com/", TimeoutSeconds = segundos });

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo) =>
        new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private sealed class Transporte(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int Envios { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Envios++;
            return responder(request, cancellationToken);
        }
    }
}
