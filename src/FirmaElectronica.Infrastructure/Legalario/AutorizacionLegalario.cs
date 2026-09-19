using System.Text.Json;
namespace FirmaElectronica.Infrastructure.Legalario;
public sealed record CredencialesLegalario(string ClientId, string ClientSecret);
public sealed class AutorizacionLegalario(HttpClient http, LegalarioOptions opciones)
{
    public async Task<string?> IniciarSesionAsync(string usuario, string contrasena, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 100 || string.IsNullOrEmpty(contrasena) || contrasena.Length > 4096) return null;
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, opciones.BaseUrl.TrimEnd('/') + "/auth/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = usuario, ["password"] = contrasena })
        };
        solicitud.Headers.Accept.ParseAdd("application/json");
        using var respuesta = await http.SendAsync(solicitud, ct);
        if (respuesta.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) return null;
        if (!respuesta.IsSuccessStatusCode) throw new InvalidOperationException("No fue posible iniciar sesión con Legalario.");
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsByteArrayAsync(ct));
        var raiz = json.RootElement;
        if (raiz.TryGetProperty("success", out var exito) && exito.ValueKind == JsonValueKind.False) return null;
        if (!raiz.TryGetProperty("data", out var datos) ||
            !datos.TryGetProperty("client_id", out var id) || id.ValueKind != JsonValueKind.String ||
            !datos.TryGetProperty("client_secret", out var secreto) || secreto.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(id.GetString()) || string.IsNullOrWhiteSpace(secreto.GetString()))
            throw new InvalidOperationException("Legalario no entregó las credenciales de sesión.");
        var credenciales = new CredencialesLegalario(id.GetString()!, secreto.GetString()!);
        return await ObtenerTokenAsync(credenciales, ct, "customers");
    }

    public async Task<string> ObtenerTokenAsync(CredencialesLegalario credenciales, CancellationToken ct, string scope = "[sdk,services,documents,signers,signature,attachments,envelopes,demo]")
    {
        if (string.IsNullOrWhiteSpace(credenciales.ClientId) || string.IsNullOrWhiteSpace(credenciales.ClientSecret))
            throw new InvalidOperationException("Falta configurar la cuenta Legalario del usuario.");
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, opciones.BaseUrl.TrimEnd('/') + "/auth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = credenciales.ClientId, ["client_secret"] = credenciales.ClientSecret,
                ["grant_type"] = "client_credentials", ["scope"] = scope
            })
        };
        solicitud.Headers.Accept.ParseAdd("application/json");
        using var respuesta = await http.SendAsync(solicitud, ct);
        if (!respuesta.IsSuccessStatusCode) throw new InvalidOperationException($"Legalario rechazó la solicitud de token de sesión (HTTP {(int)respuesta.StatusCode}).");
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsByteArrayAsync(ct));
        if (!json.RootElement.TryGetProperty("data", out var datos) || !datos.TryGetProperty("access_token", out var token) ||
            token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()))
            throw new InvalidOperationException("Legalario no entregó una autorización válida.");
        return token.GetString()!;
    }
}
