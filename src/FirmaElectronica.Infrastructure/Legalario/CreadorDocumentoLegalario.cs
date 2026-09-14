using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;

namespace FirmaElectronica.Infrastructure.Legalario;

public sealed class CreadorDocumentoLegalario : ICreadorDocumentoLegalario
{
    private readonly HttpClient cliente;
    private readonly Uri destino;
    private readonly TimeSpan tiempoLimite;

    // Usar un HttpClient dedicado, sin políticas de reintento ni redirecciones automáticas.
    public CreadorDocumentoLegalario(HttpClient cliente, LegalarioOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(opciones);
        if (!Uri.TryCreate(opciones.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
            throw new ArgumentException("La URL de Legalario debe ser una dirección HTTPS sin credenciales ni parámetros.");
        if (opciones.TimeoutSeconds <= 0)
            throw new ArgumentException("El tiempo de espera debe ser mayor que cero.");

        this.cliente = cliente;
        destino = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/v2/documents");
        tiempoLimite = TimeSpan.FromSeconds(opciones.TimeoutSeconds);
    }

    public async Task<DocumentoGenerado> CrearDocumentoAsync(
        DocumentoParaCrear documento, string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentException.ThrowIfNullOrWhiteSpace(documento.Referencia);
        ArgumentException.ThrowIfNullOrWhiteSpace(documento.Nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(documento.PlantillaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(documento.Variables);
        var variables = documento.Variables.OrderBy(par => par.Key).ToArray();
        if (variables.Length == 0 || variables.Where((par, indice) => par.Key != indice + 1 || par.Value is null).Any())
            throw new ArgumentException("Las variables deben tener posiciones consecutivas desde 1 y valores de texto.");

        cancellationToken.ThrowIfCancellationRequested();
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, destino);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        solicitud.Content = JsonContent.Create(new
        {
            name = documento.Nombre,
            type = "template",
            template_id = documento.PlantillaId,
            sequence = variables.Select(par => new[] { new { key = par.Key, value = par.Value } }).ToArray()
        });
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(tiempoLimite);
        var iniciadoEn = DateTimeOffset.UtcNow;
        try
        {
            // Un solo envío. Un timeout no demuestra que Legalario haya descartado la creación.
            using var respuesta = await cliente.SendAsync(solicitud, limite.Token);
            if (!respuesta.IsSuccessStatusCode)
            {
                var codigo = (int)respuesta.StatusCode;
                throw new CreacionDocumentoException(
                    "Legalario no confirmó la creación del documento.",
                    resultadoIncierto: codigo >= 500 || codigo is 408 || codigo < 400,
                    estadoHttp: respuesta.StatusCode);
            }

            using var contenido = await JsonDocument.ParseAsync(
                await respuesta.Content.ReadAsStreamAsync(limite.Token), cancellationToken: limite.Token);
            var raiz = contenido.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || (raiz.TryGetProperty("success", out var exito) && exito.ValueKind == JsonValueKind.False)
                || !raiz.TryGetProperty("data", out var datos) || datos.ValueKind != JsonValueKind.Object
                || !datos.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(id.GetString()))
                throw new CreacionDocumentoException("Legalario respondió sin confirmar el identificador del documento.", true);

            return new DocumentoGenerado(documento.Referencia, documento.Nombre, id.GetString()!, iniciadoEn);
        }
        catch (OperationCanceledException)
        {
            throw new CreacionDocumentoException("La espera se interrumpió. Consulte si el documento se creó antes de volver a generarlo.", true);
        }
        catch (HttpRequestException)
        {
            throw new CreacionDocumentoException("Se perdió la comunicación con Legalario. Consulte el documento antes de volver a generarlo.", true);
        }
        catch (JsonException)
        {
            throw new CreacionDocumentoException("Legalario devolvió una respuesta que no permite confirmar la creación.", true);
        }
        catch (IOException)
        {
            throw new CreacionDocumentoException("La respuesta de Legalario se interrumpió. Consulte el documento antes de volver a generarlo.", true);
        }
    }
}
