using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;
namespace FirmaElectronica.Application.Services;
public sealed class ConciliacionDocumentos(IRegistroIntentos registro, ConsultaDocumentos documentos)
{
    public async Task<IReadOnlyList<JsonElement>> CandidatosAsync(string clave, string token, CancellationToken ct)
    {
        var intento = await registro.LeerAsync(clave, ct) ?? throw new KeyNotFoundException("No existe el intento.");
        var pagina = await documentos.ConsultarAsync([intento.PlantillaId], 1, 100, intento.Nombre, token, ct);
        if (pagina.Total > 100) throw new InvalidOperationException("La búsqueda de conciliación es ambigua y requiere revisión.");
        return pagina.Documentos.Where(d => d.TryGetProperty("name", out var n) && n.GetString() == intento.Nombre &&
            ConsultaDocumentos.FechaCreacion(d) >= intento.IniciadoEn.AddSeconds(-5)).ToArray();
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
