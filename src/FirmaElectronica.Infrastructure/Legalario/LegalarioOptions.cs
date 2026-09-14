namespace FirmaElectronica.Infrastructure.Legalario;

public sealed class LegalarioOptions
{
    public const string SectionName = "Legalario";

    public string BaseUrl { get; init; } = "https://api.legalario.com";

    public int TimeoutSeconds { get; init; } = 120;
}
