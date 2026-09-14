using FirmaElectronica.Domain.Documentos;
namespace FirmaElectronica.Application.Abstractions;
public sealed record IntentoDocumento(string Clave, string Huella, string Referencia, string Nombre, string PlantillaId,
    DateTimeOffset IniciadoEn, string Estado, DocumentoGenerado? Documento);
public interface IRegistroIntentos
{
    Task<T> ExclusivoAsync<T>(string clave, Func<CancellationToken, Task<T>> operacion, CancellationToken ct);
    Task<IntentoDocumento?> LeerAsync(string clave, CancellationToken ct);
    Task<string?> ReferenciaDocumentoAsync(string documentoId, CancellationToken ct) => Task.FromResult<string?>(null);
    Task GuardarAsync(IntentoDocumento intento, CancellationToken ct);
}
