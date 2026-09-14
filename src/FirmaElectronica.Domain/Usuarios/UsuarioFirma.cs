using System.Text.RegularExpressions;
using FirmaElectronica.Domain.Agencias;
namespace FirmaElectronica.Domain.Usuarios;
public sealed record UsuarioFirma(string Usuario, string Nombre, string Rol, string Agencias)
{
    public IReadOnlyCollection<string> AgenciasPermitidas => Agencias.Trim().Equals("TODAS", StringComparison.OrdinalIgnoreCase)
        ? CatalogoAgencias.Todas.Keys.ToArray()
        : Regex.Split(Agencias.ToUpperInvariant(), @"[,;\s]+").Where(CatalogoAgencias.Todas.ContainsKey).Distinct().ToArray();
    public void ValidarAgencia(string agencia)
    {
        if (!AgenciasPermitidas.Contains(agencia.Trim().ToUpperInvariant()))
            throw new UnauthorizedAccessException("No tiene acceso a esta agencia.");
    }
}
