using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace FirmaElectronica.Application.Services;

public static class IdentidadDocumento
{
    // Legalario puede devolver el título sin diacríticos. Comparamos el título
    // completo; nunca aceptamos sólo una coincidencia parcial del cliente o VIN.
    public static bool Coincide(string? recibido, string esperado) =>
        !string.IsNullOrWhiteSpace(recibido) && Normalizar(recibido) == Normalizar(esperado);
    private static string Normalizar(string valor)
    {
        var sinAcentos = new string(valor.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(sinAcentos.Trim(), @"\s+", " ").ToUpperInvariant();
    }
}
