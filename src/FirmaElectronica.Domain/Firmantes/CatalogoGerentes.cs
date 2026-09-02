namespace FirmaElectronica.Domain.Firmantes;

public static class CatalogoGerentes
{
    public static readonly IReadOnlyDictionary<string, Firmante> Nuevos = new Dictionary<string, Firmante>
    {
        ["457"] = Gerente("CRISTOFER LARETY TORRES GARCIA", "457.gerente.ventas@nissanpachuca.com.mx", "4941005907"),
        ["457E"] = Gerente("CRISTOFER LARETY TORRES GARCIA", "457.gerente.ventas@nissanpachuca.com.mx", "4941005907"),
        ["458"] = Gerente("JANET RAMIREZ ALMEYDA", "458.gerencia.ventas@nissantulancingo.com.mx", "7751400362"),
        ["459"] = Gerente("DARIO ERNESTO ORONZOR CALVA", "459.gerencia.ventas@nissanhuauchinango.com.mx", "2211198841"),
        ["459T"] = Gerente("DARIO ERNESTO ORONZOR CALVA", "459.gerencia.ventas@nissanhuauchinango.com.mx", "2211198841"),
        ["306"] = Gerente("ARGENIS RODRIGUEZ ZAMUDIO", "306.gerencia.ventas@nissanapizaco.com.mx", "2411484206"),
        ["306T"] = Gerente("ARGENIS RODRIGUEZ ZAMUDIO", "306.gerencia.ventas@nissanapizaco.com.mx", "2411484206"),
        ["472"] = Gerente("LEONARDO WALLACE RUISANCHEZ", "472.gerente.ventas@nissannamiangelopolis.com.mx", "2211125569"),
        ["475"] = Gerente("MA DE LOS ANGELES BASILIO FERNANDEZ", "475.gerente.ventas@nissannamisanmanuel.com.mx", "2211125568"),
        ["474"] = Gerente("JOSE EMILIO CAJICA ROSAS", "474.gerente.ventas@nissannamicholula.com.mx", "2211125599"),
        ["527"] = Gerente("VICTOR MANUEL BENHUMEA MEZA", "527.gerente.ventas@nissannamitula.com.mx", "7731420047"),
        ["527T"] = Gerente("VICTOR MANUEL BENHUMEA MEZA", "527.gerente.ventas@nissannamitula.com.mx", "7731420047"),
        ["528"] = Gerente("FERNANDO VILLA CABALLERO", "528.gerente.ventas@nissannamizumpango.com.mx", "7737360717"),
        ["528J"] = Gerente("JOSE IVAN NOGUEZ LAGUNAS", "coordinador.jilotepec@nissannamitula.com.mx", "7731821628"),
        ["528T"] = Gerente("FERNANDO VILLA CABALLERO", "528.gerente.ventas@nissannamizumpango.com.mx", "7737360717"),
        ["B20ABMS009"] = Gerente("WILVER ISAAC GATICA SUAREZ", "gtecomercial@hyundaicoacalco.com.mx", "2225101606"),
        ["B20ABPA002"] = Gerente("MARIBEL PASCUAL PEREZ", "gtecomercial@hyundaipachuca.com.mx", "7711418035")
    };

    public static readonly IReadOnlyDictionary<string, Firmante> Seminuevos = new Dictionary<string, Firmante>
    {
        ["306"] = Gerente("JOSE ADAN CARCANO SANLUIS", "306.gerencia.seminuevos@nissanapizaco.com.mx", "2411986738"),
        ["306T"] = Gerente("JOSE ADAN CARCANO SANLUIS", "306.gerencia.seminuevos@nissanapizaco.com.mx", "2411986738"),
        ["457"] = Gerente("MANUEL ALEJANDRO HERNANDEZ HERNANDEZ", "457.gerencia.seminuevos@nissanpachuca.com.mx", "7712203041"),
        ["457E"] = Gerente("MANUEL ALEJANDRO HERNANDEZ HERNANDEZ", "457.gerencia.seminuevos@nissanpachuca.com.mx", "7712203041"),
        ["458"] = Gerente("MANUEL ALEJANDRO HERNANDEZ HERNANDEZ", "457.gerencia.seminuevos@nissanpachuca.com.mx", "7712203041"),
        ["459"] = Gerente("JORGE LOYOLA DIEZ", "306.gerencia.seminuevos@nissanapizaco.com.mx", "2411986738"),
        ["472"] = Gerente("JOSHUA DUQUE ROMERO", "472.seminuevos@nissannamiangelopolis.com.mx", "2211125553"),
        ["474"] = Gerente("CESAR AUGUSTO ROJAS SANCHEZ", "475.gerente.seminuevos@nissannamisanmanuel.com.mx", "2224294422"),
        ["475"] = Gerente("CESAR AUGUSTO ROJAS SANCHEZ", "475.gerente.seminuevos@nissannamisanmanuel.com.mx", "2224294422"),
        ["527"] = Gerente("JOSE LUIS FLORES SANCHEZ", "527.gerente.seminuevos@nissannamitula.com.mx", "7712949683"),
        ["528"] = Gerente("JOSE LUIS FLORES SANCHEZ", "527.gerente.seminuevos@nissannamitula.com.mx", "7712949683")
    };

    public static Firmante RepresentanteLegal { get; } =
        new("LAURA YARETH SILVA HERNANDEZ", "juridico2@grupohuerpel.com.mx", "2461128338", TipoFirmante.RepresentanteLegal);

    private static Firmante Gerente(string nombre, string correo, string telefono) =>
        new(nombre, correo, telefono, TipoFirmante.GerenteDeVentas);
}
