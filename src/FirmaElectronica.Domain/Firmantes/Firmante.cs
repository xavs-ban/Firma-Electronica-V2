namespace FirmaElectronica.Domain.Firmantes;

public sealed record Firmante(
    string Nombre,
    string Correo,
    string Telefono,
    TipoFirmante TipoFirmante,
    bool FirmaEnTodasLasHojas = false);
