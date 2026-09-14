namespace FirmaElectronica.Infrastructure.Datos;
public sealed class DatosOptions
{
    public string ConnectionString { get; init; } = "";
    public int TimeoutSeconds { get; init; } = 30;
}
