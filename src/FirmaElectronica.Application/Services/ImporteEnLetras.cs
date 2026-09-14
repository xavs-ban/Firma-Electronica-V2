using System.Globalization;
namespace FirmaElectronica.Application.Services;

// Conserva la redacción de la plataforma actual, incluso «UN MIL» y «UN MILLONES».
public static class ImporteEnLetras
{
    private static readonly string[] Unidades = ["CERO", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE"];
    private static readonly string[] Especiales = ["DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE"];
    private static readonly string[] Decenas = ["", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];
    private static readonly string[] Centenas = ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];
    public static string Convertir(decimal numero)
    {
        if (numero < 0) throw new ArgumentOutOfRangeException(nameof(numero));
        var texto = numero.ToString("0.############################", CultureInfo.InvariantCulture).Split('.');
        return $"{Entero(decimal.Truncate(numero))} {(texto.Length > 1 ? texto[1].PadRight(2, '0') : "00")}/100 MXN";
    }
    private static string Entero(decimal numero)
    {
        if (numero >= 1_000_000_000) return numero.ToString(CultureInfo.InvariantCulture);
        var n = (int)numero;
        if (n < 10) return Unidades[n];
        if (n < 20) return Especiales[n - 10];
        if (n < 100) return Decenas[n / 10] + (n % 10 == 0 ? "" : " Y " + Unidades[n % 10]);
        if (n < 1000) return Centenas[n / 100] + Resto(n % 100);
        if (n < 1_000_000) return Entero(n / 1000) + " MIL" + Resto(n % 1000);
        return Entero(n / 1_000_000) + " MILLONES" + Resto(n % 1_000_000);
    }
    private static string Resto(int n) => n == 0 ? "" : " " + Entero(n);
}
