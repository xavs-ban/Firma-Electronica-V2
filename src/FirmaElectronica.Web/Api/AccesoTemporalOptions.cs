namespace FirmaElectronica.Web.Api;

public sealed class AccesoTemporalOptions
{
    public bool Activo { get; init; }
    public string Usuario { get; init; } = "";
    public string Contrasena { get; init; } = "";
}
