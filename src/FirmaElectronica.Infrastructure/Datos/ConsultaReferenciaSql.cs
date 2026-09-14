using System.Data;
using Microsoft.Data.SqlClient;
using FirmaElectronica.Application.Abstractions;
namespace FirmaElectronica.Infrastructure.Datos;
public sealed class ConsultaReferenciaSql(DatosOptions opciones) : IConsultaReferencia
{
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ConsultarAsync(string procedimiento, string referencia, CancellationToken cancellationToken)
    {
        if (!new[] { "sp_ObtenerDatosReferenciaPM", "sp_ObtenerDatosReferencia", "sp_ObtenerDatosReferenciaSeminuevos" }.Contains(procedimiento))
            throw new ArgumentException("Procedimiento no permitido.");
        if (string.IsNullOrWhiteSpace(opciones.ConnectionString)) throw new InvalidOperationException("Falta configurar la conexión de datos.");
        await using var conexion = new SqlConnection(opciones.ConnectionString);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure, CommandTimeout = opciones.TimeoutSeconds };
        comando.Parameters.Add("@Referencia", SqlDbType.VarChar, 50).Value = referencia;
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        var filas = new List<IReadOnlyDictionary<string, object?>>();
        while (await lector.ReadAsync(cancellationToken))
        {
            var fila = new Dictionary<string, object?>();
            for (var i = 0; i < lector.FieldCount; i++) fila[lector.GetName(i)] = await lector.IsDBNullAsync(i, cancellationToken) ? null : lector.GetValue(i);
            filas.Add(fila);
            if (filas.Count == 2) break;
        }
        return filas;
    }
}
