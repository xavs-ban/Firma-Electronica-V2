namespace FirmaElectronica.Application.Abstractions;

public interface IReferenciaDataProvider
{
    Task<IReadOnlyDictionary<string, object?>> ObtenerDatosReferenciaAsync(string referencia, CancellationToken cancellationToken);
}
