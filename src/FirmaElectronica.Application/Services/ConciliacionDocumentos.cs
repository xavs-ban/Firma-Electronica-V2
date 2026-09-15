using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;
namespace FirmaElectronica.Application.Services;
public sealed class ConciliacionDocumentos(IRegistroIntentos registro, ConsultaDocumentos documentos)
{
    public async Task<IReadOnlyList<JsonElement>> CandidatosAsync(string clave, string token, CancellationToken ct)
    {
        var intento = await registro.LeerAsync(clave, ct) ?? throw new KeyNotFoundException("No existe el intento.");
        var candidatos = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        // El total incluye documentos ajenos al intento cuando el proveedor no filtra
        // por nombre. No implica que existan varios candidatos para este expediente.
        for (var numero = 1; numero <= 50; numero++)
        {
            var pagina = await documentos.ConsultarAsync([intento.PlantillaId], numero, 100, intento.Nombre, token, ct);
            foreach (var documento in pagina.Documentos)
                if (documento.TryGetProperty("name", out var nombre) && nombre.ValueKind == JsonValueKind.String &&
                    IdentidadDocumento.Coincide(nombre.GetString(), intento.Nombre) &&
                    ConsultaDocumentos.FechaCreacion(documento) >= intento.IniciadoEn.AddSeconds(-5) &&
                    documento.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(id.GetString()))
                    candidatos[id.GetString()!] = documento;
            if ((long)numero * 100 >= pagina.Total)
                return candidatos.Values.OrderByDescending(ConsultaDocumentos.FechaCreacion).ToArray();
        }
        throw new InvalidOperationException("No se pudo completar la revisión de documentos. La consulta supera el límite de recuperación; contacte a soporte.");
    }
    public Task<DocumentoGenerado> ConfirmarAsync(string clave, string documentoId, string token, CancellationToken ct) => registro.ExclusivoAsync(clave, async cancelacion =>
    {
        var intento = await registro.LeerAsync(clave, cancelacion) ?? throw new KeyNotFoundException("No existe el intento.");
        if (intento.Documento is not null)
        {
            if (intento.Documento.LegalarioDocumentId != documentoId) throw new InvalidOperationException("El intento ya está asociado a otro documento.");
            return intento.Documento;
        }
        var candidatos = await CandidatosAsync(clave, token, cancelacion);
        var elegido = candidatos.SingleOrDefault(d => d.GetProperty("id").GetString() == documentoId);
        if (elegido.ValueKind == JsonValueKind.Undefined) throw new ArgumentException("El documento no corresponde a los candidatos del intento.");
        var documento = new DocumentoGenerado(intento.Referencia, intento.Nombre, documentoId, ConsultaDocumentos.FechaCreacion(elegido));
        await registro.GuardarAsync(intento with { Estado = "Conciliado", Documento = documento }, cancelacion);
        return documento;
    }, ct);
}
