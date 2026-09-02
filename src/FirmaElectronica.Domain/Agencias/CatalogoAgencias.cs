namespace FirmaElectronica.Domain.Agencias;

public static class CatalogoAgencias
{
    public static readonly IReadOnlyDictionary<string, Agencia> Todas = new Dictionary<string, Agencia>
    {
        ["306"] = new("306", "APIZACO", "Nissan"),
        ["457"] = new("457", "PACHUCA", "Nissan"),
        ["457E"] = new("457E", "EXPLANADA", "Nissan"),
        ["458"] = new("458", "TULANCINGO", "Nissan"),
        ["459"] = new("459", "HUAUCHINANGO", "Nissan"),
        ["459T"] = new("459T", "ZACATLAN", "Nissan"),
        ["472"] = new("472", "ANGELOPOLIS", "Nissan"),
        ["474"] = new("474", "CHOLULA", "Nissan"),
        ["475"] = new("475", "SAN MANUEL", "Nissan"),
        ["527"] = new("527", "TULA", "Nissan"),
        ["527T"] = new("527T", "IXMIQUILPAN", "Nissan"),
        ["528"] = new("528", "ZUMPANGO", "Nissan"),
        ["528J"] = new("528J", "JILOTEPEC", "Nissan"),
        ["528T"] = new("528T", "TIZAYUCA", "Nissan"),
        ["306T"] = new("306T", "NAMI TLAXCALA", "Nissan"),
        ["B20ABMS009"] = new("B20ABMS009", "HYUNDAI COACALCO", "Hyundai", EsHyundai: true),
        ["B20ABPA002"] = new("B20ABPA002", "HYUNDAI PACHUCA", "Hyundai", EsHyundai: true)
    };

    public static bool EsHyundai(string agencia) =>
        Todas.TryGetValue(agencia.Trim().ToUpperInvariant(), out var item) && item.EsHyundai;
}
