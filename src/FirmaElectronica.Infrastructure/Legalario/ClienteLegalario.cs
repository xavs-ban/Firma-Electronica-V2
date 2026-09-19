using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Firmantes;
namespace FirmaElectronica.Infrastructure.Legalario;

public sealed class ClienteLegalario(HttpClient http, LegalarioOptions opciones) : ILegalarioClient
{
    private readonly CreadorDocumentoLegalario creador = new(http, opciones);
    public Task<DocumentoGenerado> CrearDocumentoAsync(DocumentoParaCrear documento, string token, CancellationToken ct) => creador.CrearDocumentoAsync(documento, token, ct);
    private static string Codificar(string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);
        return Uri.EscapeDataString(valor);
    }
    private HttpRequestMessage Solicitud(HttpMethod metodo, string ruta, string token, object? cuerpo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var solicitud = new HttpRequestMessage(metodo, opciones.BaseUrl.TrimEnd('/') + ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo);
        return solicitud;
    }
    private async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage solicitud, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(opciones.TimeoutSeconds));
        var mutacion = solicitud.Method != HttpMethod.Get;
        try
        {
            var respuesta = await http.SendAsync(solicitud, limite.Token);
            return respuesta;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested || mutacion)
        { throw new OperacionLegalarioException("Se interrumpió la comunicación con Legalario.", resultadoIncierto: mutacion, reintentable: !mutacion); }
        catch (HttpRequestException)
        { throw new OperacionLegalarioException("No se pudo completar la comunicación con Legalario.", resultadoIncierto: mutacion, reintentable: !mutacion); }
    }
    private async Task<JsonElement> JsonAsync(HttpMethod metodo, string ruta, string token, CancellationToken ct, object? cuerpo = null)
    {
        using var solicitud = Solicitud(metodo, ruta, token, cuerpo);
        using var respuesta = await EnviarAsync(solicitud, ct);
        var mutacion = metodo != HttpMethod.Get;
        var codigo = (int)respuesta.StatusCode;
        JsonElement raiz = default;
        try
        {
            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsByteArrayAsync(ct));
            raiz = json.RootElement.Clone();
        }
        catch (JsonException) { }
        catch (OperationCanceledException) when (mutacion)
        { throw new OperacionLegalarioException("Se interrumpió la respuesta de Legalario.", resultadoIncierto: true); }
        catch (IOException)
        { throw new OperacionLegalarioException("Se interrumpió la respuesta de Legalario.", resultadoIncierto: mutacion, reintentable: !mutacion); }
        var objeto = raiz.ValueKind == JsonValueKind.Object;
        var rechazado = objeto && raiz.TryGetProperty("success", out var exito) && exito.ValueKind == JsonValueKind.False;
        var repositorio = (rechazado || !respuesta.IsSuccessStatusCode) && RepositorioPendiente(raiz);
        if (!respuesta.IsSuccessStatusCode || rechazado)
        {
            // Un 5xx/408 en un POST sigue siendo incierto, incluso si menciona el repositorio.
            var incierto = mutacion && (codigo >= 500 || codigo == 408 || (codigo < 400 && !rechazado));
            var temporal = codigo is not (401 or 403) && (mutacion
                ? !incierto && (repositorio || codigo == 429)
                : repositorio || codigo is 404 or 408 or 409 or 425 or 429 or >= 500);
            throw new OperacionLegalarioException(temporal
                ? "Legalario todavía no permite completar la operación. Espera mientras termina de preparar el documento."
                : "Legalario rechazó o no confirmó la operación.", codigo, incierto, temporal);
        }
        if (respuesta.StatusCode == System.Net.HttpStatusCode.NoContent && mutacion)
            return JsonSerializer.SerializeToElement(new { success = true });
        if (!objeto)
            throw new OperacionLegalarioException("La respuesta de Legalario no tiene el formato esperado.", resultadoIncierto: mutacion);
        if (mutacion && (!raiz.TryGetProperty("success", out var confirmacion) || confirmacion.ValueKind != JsonValueKind.True))
            throw new OperacionLegalarioException("Legalario respondió sin confirmar la operación.", resultadoIncierto: true);
        return raiz;
    }
    private static bool RepositorioPendiente(JsonElement valor)
    {
        if (valor.ValueKind == JsonValueKind.String)
        {
            var mensaje = valor.GetString() ?? "";
            return mensaje.Contains("archivo no fue encontrado en el repositorio", StringComparison.OrdinalIgnoreCase)
                || mensaje.Contains("documento no está disponible en el repositorio", StringComparison.OrdinalIgnoreCase)
                || mensaje.Contains("documento no esta disponible en el repositorio", StringComparison.OrdinalIgnoreCase);
        }
        if (valor.ValueKind == JsonValueKind.Object)
            return valor.EnumerateObject().Any(p => RepositorioPendiente(p.Value));
        return valor.ValueKind == JsonValueKind.Array && valor.EnumerateArray().Any(RepositorioPendiente);
    }
    public async Task<PaginaLegalario> ConsultarPaginaAsync(string plantilla, int pagina, int cantidad, string? busqueda, string token, CancellationToken ct)
    {
        if (pagina < 1 || cantidad is < 1 or > 100) throw new ArgumentException("Paginación inválida.");
        var raiz = await JsonAsync(HttpMethod.Get, $"/v2/documents?template_id={Codificar(plantilla)}&page={pagina}&per_page={cantidad}&search={Uri.EscapeDataString(busqueda ?? "")}", token, ct);
        if (!raiz.TryGetProperty("data", out var datos) || datos.ValueKind != JsonValueKind.Object ||
            !datos.TryGetProperty("data", out var lista) || lista.ValueKind != JsonValueKind.Array)
            throw new OperacionLegalarioException("Legalario no entregó la lista de documentos.");
        var ultima = 1;
        var total = lista.GetArrayLength();
        if (datos.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            if (meta.TryGetProperty("last_page", out var u) && u.TryGetInt32(out var valor)) ultima = Math.Max(1, valor);
            if (meta.TryGetProperty("total", out var t) && t.TryGetInt32(out valor)) total = valor;
        }
        else if (lista.GetArrayLength() == cantidad)
            throw new OperacionLegalarioException("Falta la paginación de Legalario; no se mostrará una lista posiblemente incompleta.");
        return new(lista.EnumerateArray().Select(x => x.Clone()).ToArray(), ultima, total);
    }
    public async Task<JsonElement> ConsultarDocumentoAsync(string documentoId, string token, CancellationToken ct)
    {
        var raiz = await JsonAsync(HttpMethod.Get, $"/v2/documents/{Codificar(documentoId)}", token, ct);
        if (!raiz.TryGetProperty("data", out var datos) || datos.ValueKind != JsonValueKind.Object)
            throw new OperacionLegalarioException("Legalario no entregó los datos del documento.");
        return datos.Clone();
    }
    private static string? BuscarUrl(JsonElement valor)
    {
        if (valor.ValueKind == JsonValueKind.String)
        {
            var texto = valor.GetString();
            return Uri.TryCreate(texto, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) ? uri.AbsoluteUri : null;
        }
        if (valor.ValueKind == JsonValueKind.Object)
        {
            foreach (var clave in new[] { "document", "document_url", "download_url", "url", "file", "file_url", "archivo", "archivo_url", "location", "pdf", "pdf_url", "data" })
                if (valor.TryGetProperty(clave, out var hijo) && BuscarUrl(hijo) is { } url) return url;
        }
        return null;
    }
    public async Task<string?> ObtenerUrlDocumentoAsync(string documentoId, string token, CancellationToken ct)
    {
        var datos = await ConsultarDocumentoAsync(documentoId, token, ct);
        if (BuscarUrl(datos) is { } directa) return directa;
        var firmado = datos.TryGetProperty("signature_progress", out var progreso) && progreso.ValueKind != JsonValueKind.Null && progreso.ToString() != "";
        foreach (var tipo in new[] { firmado ? "Documento con firmado" : "Documento sin firmas", "Documento sin firmas", "Documento con firmas", "Documento con firmado", "Documento original" }.Distinct())
        {
            try
            {
                var resultado = await JsonAsync(HttpMethod.Get, $"/v2/documents/download?document_id={Codificar(documentoId)}&document_type={Codificar(tipo)}&format=URL", token, ct);
                if (BuscarUrl(resultado) is { } url) return url;
            }
            catch (OperacionLegalarioException e) when (e.EstadoHttp is not (401 or 403)) { }
        }
        return null;
    }
    public async Task<byte[]?> DescargarPdfAsync(string documentoId, string token, CancellationToken ct)
    {
        // Se consulta sólo el endpoint conocido. Nunca se descarga una URL arbitraria con el token.
        JsonElement datos;
        try { datos = await ConsultarDocumentoAsync(documentoId, token, ct); }
        catch (OperacionLegalarioException e) when (e.EstadoHttp is 404 or 409 or 425 or 429 or 500 or 502 or 503 or 504) { return null; }
        var firmado = datos.TryGetProperty("signature_progress", out var progreso) && progreso.ValueKind != JsonValueKind.Null && progreso.ToString() != "";
        var tipos = new[] { firmado ? "Documento con firmado" : "Documento sin firmas", "Documento sin firmas", "Documento con firmas", "Documento con firmado", "Documento original" }.Distinct();
        foreach (var tipo in tipos)
        {
            using var solicitud = Solicitud(HttpMethod.Get, $"/v2/documents/download?document_id={Codificar(documentoId)}&document_type={Codificar(tipo)}&format=PDF", token);
            solicitud.Headers.Accept.Clear();
            solicitud.Headers.Accept.ParseAdd("application/pdf,application/json");
            using var respuesta = await EnviarAsync(solicitud, ct);
            if (respuesta.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                throw new OperacionLegalarioException("No hay acceso al documento.", (int)respuesta.StatusCode);
            if (!respuesta.IsSuccessStatusCode) continue;
            var bytes = await respuesta.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) return bytes;
        }
        return null;
    }
    public async Task<EstadoFirmas> ConsultarFirmasAsync(string documentoId, string token, CancellationToken ct)
    {
        var raiz = await JsonAsync(HttpMethod.Get, $"/v2/signers?document_id={Codificar(documentoId)}", token, ct);
        if (!raiz.TryGetProperty("data", out var lista) || lista.ValueKind != JsonValueKind.Array)
            throw new OperacionLegalarioException("Legalario no entregó la lista de firmantes.");
        var firmantes = lista.EnumerateArray().Select(x => x.Clone()).ToArray();
        return new(firmantes.Count(x => x.TryGetProperty("status", out var e) && e.GetString() == "confirmed"), firmantes.Length, firmantes);
    }
    public async Task ConvocarFirmantesAsync(string documentoId, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct)
    {
        ValidadorFirmantes.Validar(firmantes);
        await JsonAsync(HttpMethod.Post, "/v2/signers", token, ct, new
        {
            document_id = documentoId, use_whatsapp = true, send_invite = true,
            signers = firmantes.Select(f => new { fullname = f.Nombre.Trim(), email = f.Correo.Trim(), phone = f.Telefono.Trim(), type = ValidadorFirmantes.Tipo(f.TipoFirmante), role = "FIRMANTE" })
        });
    }
    public async Task ReenviarInvitacionAsync(string firmanteId, string token, CancellationToken ct) =>
        await JsonAsync(HttpMethod.Post, $"/v2/signers/{Codificar(firmanteId)}/invite", token, ct, new { use_whatsapp = true });
    public async Task EliminarDocumentoAsync(string documentoId, string token, CancellationToken ct) =>
        await JsonAsync(HttpMethod.Delete, $"/v2/documents/{Codificar(documentoId)}", token, ct);
}
