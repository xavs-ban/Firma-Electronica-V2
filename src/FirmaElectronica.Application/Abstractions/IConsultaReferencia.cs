namespace FirmaElectronica.Application.Abstractions;
public interface IConsultaReferencia
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ConsultarAsync(string procedimiento, string referencia, CancellationToken cancellationToken);
}
