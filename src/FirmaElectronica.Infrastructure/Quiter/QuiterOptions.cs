namespace FirmaElectronica.Infrastructure.Quiter;
public sealed class QuiterOptions
{
    public string BaseUrl { get; init; } = "https://qis.quiter.com/qis";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string Code { get; init; } = "";
}
