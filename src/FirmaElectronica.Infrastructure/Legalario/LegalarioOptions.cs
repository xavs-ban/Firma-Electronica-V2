namespace FirmaElectronica.Infrastructure.Legalario;

public sealed class LegalarioOptions
{
    public const string SectionName = "Legalario";

    public required string BaseUrl { get; init; }

    public required string Token { get; init; }

    public int TimeoutSeconds { get; init; } = 45;
}
