using System.Data;
using Microsoft.Data.SqlClient;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Usuarios;
namespace FirmaElectronica.Infrastructure.Datos;
public sealed class ConsultaPerfilUsuarioSql(DatosOptions opciones) : IConsultaPerfilUsuario
{
    public async Task<UsuarioFirma?> ConsultarAsync(string usuario, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 100) return null;
        if (string.IsNullOrWhiteSpace(opciones.ConnectionString)) throw new InvalidOperationException("Falta configurar la conexión de datos.");
        await using var conexion = new SqlConnection(opciones.ConnectionString);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = new SqlCommand("SELECT TOP (2) USUARIOID, USUARIO, ROL, NOMBRE_USUARIO, AGENCIA FROM USUARIOS_LEGALARIO WHERE USUARIO = @usuario", conexion) { CommandTimeout = opciones.TimeoutSeconds };
        comando.Parameters.Add("@usuario", SqlDbType.NVarChar, 100).Value = usuario;
        UsuarioFirma? resultado;
        int id;
        await using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
        {
            if (!await lector.ReadAsync(cancellationToken)) return null;
            id = Convert.ToInt32(lector["USUARIOID"]);
            resultado = new(Convert.ToString(lector["USUARIO"]) ?? "", Convert.ToString(lector["NOMBRE_USUARIO"]) ?? "", Convert.ToString(lector["ROL"]) ?? "", Convert.ToString(lector["AGENCIA"]) ?? "");
            if (await lector.ReadAsync(cancellationToken)) return null;
        }
        await using var acceso = new SqlCommand("UPDATE USUARIOS_LEGALARIO SET ULTIMO_ACCESO = @fecha WHERE USUARIOID = @id", conexion) { CommandTimeout = opciones.TimeoutSeconds };
        acceso.Parameters.Add("@id", SqlDbType.Int).Value = id;
        acceso.Parameters.Add("@fecha", SqlDbType.NVarChar, 19).Value = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Mexico_City").ToString("yyyy-MM-dd HH:mm:ss");
        await acceso.ExecuteNonQueryAsync(cancellationToken);
        return resultado;
    }
}
