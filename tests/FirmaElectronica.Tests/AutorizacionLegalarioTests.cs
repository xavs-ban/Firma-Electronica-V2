using System.Net;
using System.Net.Http.Json;
using FirmaElectronica.Infrastructure.Legalario;
namespace FirmaElectronica.Tests;
public class AutorizacionLegalarioTests
{
    [Fact]
    public async Task ObtieneCredencialesDelLoginYUsaScopeDeLaPlataformaActual()
    {
        var rutas = new List<string>();
        using var http = new HttpClient(new Transporte(async solicitud =>
        {
            Assert.Contains(solicitud.Headers.Accept, valor => valor.MediaType == "application/json");
            var cuerpo = await solicitud.Content!.ReadAsStringAsync();
            rutas.Add(solicitud.RequestUri!.AbsolutePath);
            if (rutas.Count == 1)
            {
                Assert.Equal("/auth/login", rutas[0]);
                Assert.Contains("email=prueba%40example.com", cuerpo);
                Assert.Contains("password=clave%2B%26", cuerpo);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, data = new { client_id = "id-remoto", client_secret = "secreto-remoto" } }) };
            }
            Assert.Equal("/auth/token", rutas[1]);
            Assert.Contains("client_id=id-remoto", cuerpo);
            Assert.Contains("client_secret=secreto-remoto", cuerpo);
            Assert.Contains("scope=customers", cuerpo);
            Assert.Contains("grant_type=client_credentials", cuerpo);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new { access_token = "token" } }) };
        }));
        Assert.Equal("token", await new AutorizacionLegalario(http, new()).IniciarSesionAsync("prueba@example.com", "clave+&", default));
        Assert.Equal(2, rutas.Count);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(200)]
    public async Task RechazoNoSolicitaToken(int estado)
    {
        var llamadas = 0;
        using var http = new HttpClient(new Transporte(_ =>
        {
            llamadas++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)estado) { Content = JsonContent.Create(new { success = false }) });
        }));
        Assert.Null(await new AutorizacionLegalario(http, new()).IniciarSesionAsync("prueba", "incorrecta", default));
        Assert.Equal(1, llamadas);
    }
    private sealed class Transporte(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
}
