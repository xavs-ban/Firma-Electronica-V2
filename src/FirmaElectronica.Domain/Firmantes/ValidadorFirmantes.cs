using System.Text.RegularExpressions;
namespace FirmaElectronica.Domain.Firmantes;
public static class ValidadorFirmantes
{
    public static string Tipo(TipoFirmante tipo) => tipo switch
    {
        TipoFirmante.Cliente => "CLIENTE", TipoFirmante.Apv => "APV",
        TipoFirmante.GerenteDeVentas => "GERENTE DE VENTAS", TipoFirmante.RepresentanteLegal => "REPRESENTANTE LEGAL",
        _ => throw new ArgumentException("Tipo de firmante inválido.")
    };
    public static bool CorreoValido(string correo) => correo.Length <= 254 &&
        System.Net.Mail.MailAddress.TryCreate(correo, out var direccion) && direccion.Address == correo &&
        Regex.IsMatch(correo, @"^[^\s@.]+(?:\.[^\s@.]+)*@[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)+$");
    public static void Validar(IReadOnlyCollection<Firmante> firmantes)
    {
        if (firmantes.Count == 0) throw new ArgumentException("Faltan los firmantes.");
        foreach (var f in firmantes)
        {
            _ = Tipo(f.TipoFirmante);
            if (string.IsNullOrWhiteSpace(f.Nombre) || f.Nombre == "Nombre no disponible" || string.IsNullOrWhiteSpace(f.Correo) ||
                !CorreoValido(f.Correo.Trim()) || string.IsNullOrWhiteSpace(f.Telefono) ||
                !Regex.IsMatch(f.Telefono, @"^[0-9]{10}$"))
                throw new ArgumentException("Complete el nombre, un correo válido y un teléfono de exactamente 10 dígitos para cada firmante.");
            if (f.FirmaEnTodasLasHojas) throw new ArgumentException("La opción de firma en todas las hojas requiere confirmar el contrato de Legalario.");
        }
    }
}
