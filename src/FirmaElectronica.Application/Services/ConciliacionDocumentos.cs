using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;
namespace FirmaElectronica.Application.Services;
public sealed class ConciliacionDocumentos(IRegistroIntentos registro, ILegalarioClient legalario)
{
    public async Task<IReadOnlyList<JsonElement>> CandidatosAsync(string clave, string token, CancellationToken ct)
    {
        var intento = await registro.LeerAsync(clave, ct) ?? throw new KeyNotFoundException("No existe el intento.");
        var candidatos = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        // Buscar por VIN evita que los acentos del prefijo alteren el filtro remoto.
        // La identidad completa se verifica aquí y el usuario revisa el PDF antes de asociar.
        var vin = intento.Nombre.Split('_').LastOrDefault();
        var busqueda = vin is not null && System.Text.RegularExpressions.Regex.IsMatch(vin, "^[A-HJ-NPR-Z0-9]{17}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            ? vin : null;
        var ultima = 1;
        for (var paso = 0; paso < 50; paso++)
        {
            var numero = paso == 0 ? 1 : ultima - paso + 1;
            if (numero < 2 && paso > 0) break;
            var pagina = await legalario.ConsultarPaginaAsync(intento.PlantillaId, numero, 100, busqueda, token, ct);
            if (paso == 0) ultima = pagina.UltimaPagina;
            foreach (var documento in pagina.Documentos)
                if (documento.TryGetProperty("name", out var nombre) && nombre.ValueKind == JsonValueKind.String &&
                    IdentidadDocumento.Coincide(nombre.GetString(), intento.Nombre) &&
                    documento.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(id.GetString()))
                    candidatos[id.GetString()!] = documento;
            if (candidatos.Count > 0 || ultima == 1)
                return candidatos.Values.OrderByDescending(ConsultaDocumentos.FechaCreacion).ToArray();
        }
        return candidatos.Values.OrderByDescending(ConsultaDocumentos.FechaCreacion).ToArray();
    }
    public Task<DocumentoGenerado> ConfirmarAsync(string clave, string documentoId, string token, CancellationToken ct) => registro.ExclusivoAsync(clave, async cancelacion =>
    {
        var intento = await registro.LeerAsync(clave, cancelacion) ?? throw new KeyNotFoundException("No existe el intento.");
        if (intento.Documento is not null)
        {
            if (intento.Documento.LegalarioDocumentId != documentoId) throw new InvalidOperationException("El intento ya está asociado a otro documento.");
            return intento.Documento;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(documentoId);
        var elegido = await legalario.ConsultarDocumentoAsync(documentoId, token, cancelacion);
        var plantilla = elegido.TryGetProperty("organization_document_id", out var org) ? org.ToString()
            : elegido.TryGetProperty("template_id", out var tpl) ? tpl.ToString() : "";
        if (!elegido.TryGetProperty("id", out var id) || id.GetString() != documentoId ||
            !elegido.TryGetProperty("name", out var nombre) || nombre.ValueKind != JsonValueKind.String ||
            !IdentidadDocumento.Coincide(nombre.GetString(), intento.Nombre) || plantilla != intento.PlantillaId)
            throw new ArgumentException("El documento no corresponde al nombre y plantilla de este expediente.");
        var documento = new DocumentoGenerado(intento.Referencia, intento.Nombre, documentoId, ConsultaDocumentos.FechaCreacion(elegido));
        await registro.GuardarAsync(intento with { Estado = "Conciliado", Documento = documento }, cancelacion);
        return documento;
    }, ct);
}
