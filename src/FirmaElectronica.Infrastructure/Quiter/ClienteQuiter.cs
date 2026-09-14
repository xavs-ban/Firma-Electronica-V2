using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FirmaElectronica.Application.Abstractions;
namespace FirmaElectronica.Infrastructure.Quiter;
public sealed class ClienteQuiter(HttpClient http, QuiterOptions opciones) : IQuiterClient
{
    public static IReadOnlyDictionary<string, object> PrepararContacto(ContactoClienteQuiter contacto)
    {
        var resultado = new Dictionary<string, object>();
        var telefono = Regex.Replace(contacto.Telefono ?? "", @"\D", "");
        if (telefono.Length > 10) telefono = telefono[^10..];
        if (!string.IsNullOrWhiteSpace(contacto.Correo)) resultado["email"] = contacto.Correo.Trim();
        if (telefono.Length > 0)
        {
            resultado["phoneNumbers"] = new[] { new { phoneNumber = telefono, observations = "ACTUALIZADO DESDE FIRMA DIGITAL" } };
            resultado["mobilePhoneNumber"] = new[] { new { phoneNumber = telefono, observations = "ACTUALIZADO DESDE FIRMA DIGITAL" } };
        }
        if (resultado.Count > 0) resultado["validated"] = true;
        return resultado;
    }
    public async Task ActualizarContactoClienteAsync(ContactoClienteQuiter contacto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contacto.CuentaCliente)) return;
        var cuerpo = PrepararContacto(contacto);
        if (cuerpo.Count == 0) return;
        if (string.IsNullOrWhiteSpace(opciones.ClientId) || string.IsNullOrWhiteSpace(opciones.ClientSecret) || string.IsNullOrWhiteSpace(opciones.Code))
            throw new InvalidOperationException("Falta configurar el acceso a Quiter.");
        if (!Uri.TryCreate(opciones.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https" || baseUri.UserInfo != "" || baseUri.Query != "" || baseUri.Fragment != "")
            throw new InvalidOperationException("La dirección de Quiter debe usar HTTPS sin credenciales ni parámetros.");
        using var autorizacion = new HttpRequestMessage(HttpMethod.Post, opciones.BaseUrl.TrimEnd('/') + "/oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = opciones.ClientId, ["client_secret"] = opciones.ClientSecret,
                ["grant_type"] = "authorization_code", ["code"] = opciones.Code
            })
        };
        autorizacion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var respuestaToken = await http.SendAsync(autorizacion, cancellationToken);
        if (!respuestaToken.IsSuccessStatusCode) throw new InvalidOperationException("Quiter no autorizó la actualización de contacto.");
        using var json = JsonDocument.Parse(await respuestaToken.Content.ReadAsByteArrayAsync(cancellationToken));
        var raiz = json.RootElement;
        string? token = null;
        foreach (var clave in new[] { "access_token", "token", "accessToken" })
            if (raiz.TryGetProperty(clave, out var t) && t.ValueKind == JsonValueKind.String) token = t.GetString();
        if (token is null && raiz.TryGetProperty("data", out var datos) && datos.ValueKind == JsonValueKind.Object && datos.TryGetProperty("access_token", out var anidado)) token = anidado.GetString();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Quiter no entregó una autorización válida.");
        using var solicitud = new HttpRequestMessage(HttpMethod.Put, opciones.BaseUrl.TrimEnd('/') + "/api/customers/v1/customers/" + Uri.EscapeDataString(contacto.CuentaCliente))
        { Content = JsonContent.Create(cuerpo) };
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await http.SendAsync(solicitud, cancellationToken);
        if (!respuesta.IsSuccessStatusCode) throw new InvalidOperationException("Quiter no confirmó la actualización de contacto.");
    }
}
