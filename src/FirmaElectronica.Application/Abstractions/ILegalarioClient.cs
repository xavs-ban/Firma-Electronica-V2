using System.Text.Json;
using FirmaElectronica.Domain.Firmantes;
namespace FirmaElectronica.Application.Abstractions;

public sealed record PaginaLegalario(IReadOnlyList<JsonElement> Documentos, int UltimaPagina, int Total);
public sealed record EstadoFirmas(int Firmados, int Convocados, IReadOnlyList<JsonElement> Firmantes);
public interface ILegalarioClient : ICreadorDocumentoLegalario
{
    Task<PaginaLegalario> ConsultarPaginaAsync(string plantilla, int pagina, int cantidad, string? busqueda, string token, CancellationToken ct);
    Task<JsonElement> ConsultarDocumentoAsync(string documentoId, string token, CancellationToken ct);
    Task<string?> ObtenerUrlDocumentoAsync(string documentoId, string token, CancellationToken ct) => Task.FromResult<string?>(null);
    Task<byte[]?> DescargarPdfAsync(string documentoId, string token, CancellationToken ct);
    Task<EstadoFirmas> ConsultarFirmasAsync(string documentoId, string token, CancellationToken ct);
    Task ConvocarFirmantesAsync(string documentoId, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct);
    Task ReenviarInvitacionAsync(string firmanteId, string token, CancellationToken ct);
    Task EliminarDocumentoAsync(string documentoId, string token, CancellationToken ct);
}
public sealed class OperacionLegalarioException(string mensaje, int? estadoHttp = null, bool resultadoIncierto = false, bool reintentable = false) : Exception(mensaje)
{
    public int? EstadoHttp { get; } = estadoHttp;
    public bool ResultadoIncierto { get; } = resultadoIncierto;
    // Sólo lecturas temporales o rechazos que confirman que no se ejecutó la mutación.
    public bool Reintentable { get; } = reintentable && !resultadoIncierto;
}
