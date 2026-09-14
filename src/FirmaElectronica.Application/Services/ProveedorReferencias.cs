using FirmaElectronica.Application.Abstractions;
namespace FirmaElectronica.Application.Services;
public sealed class ProveedorReferencias(IConsultaReferencia consulta) : IReferenciaDataProvider
{
    public async Task<IReadOnlyDictionary<string, object?>> ObtenerDatosReferenciaAsync(string referencia, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referencia);
        if (referencia.Trim().Length > 50) throw new ArgumentException("La referencia excede 50 caracteres.");
        if (referencia.Trim().Any(c => c < '0' || c > '9')) throw new ArgumentException("La referencia sólo acepta números.");
        string[] procedimientos = ["sp_ObtenerDatosReferenciaPM", "sp_ObtenerDatosReferencia", "sp_ObtenerDatosReferenciaSeminuevos"];
        string[] tipos = ["PERSONA_MORAL", "PERSONA_FISICA", "SEMINUEVO"];
        for (var i = 0; i < procedimientos.Length; i++)
        {
            var filas = await consulta.ConsultarAsync(procedimientos[i], referencia.Trim(), cancellationToken);
            if (filas.Count > 1) throw new InvalidOperationException("La referencia tiene más de un registro. Revise el expediente.");
            if (filas.Count == 0) continue;
            return new Dictionary<string, object?>(filas[0])
            {
                ["isPersonaMoral"] = i == 0, ["isPersonaFisica"] = i == 1, ["isSeminuevo"] = i == 2,
                ["tipoExpediente"] = tipos[i], ["spOrigen"] = procedimientos[i]
            };
        }
        throw new KeyNotFoundException("No se encontró la referencia.");
    }
}
