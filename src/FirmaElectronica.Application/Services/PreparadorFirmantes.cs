using FirmaElectronica.Domain.Firmantes;
using FirmaElectronica.Domain.Plantillas;
namespace FirmaElectronica.Application.Services;
public sealed class ReglasFirmantes
{
    // Acuerdo confirmado: Hyundai incluye al representante legal.
    public bool IncluirRepresentanteHyundai { get; init; } = true;
}
public sealed class PreparadorFirmantes(ReglasFirmantes opciones)
{
    public IReadOnlyList<Firmante> Preparar(ReglaPlantilla regla, IReadOnlyDictionary<string, object?> datos)
    {
        var incluir = regla.Tipo == TipoPlantilla.Hyundai
            ? opciones.IncluirRepresentanteHyundai
            : regla.IncluyeRepresentanteLegal;
        var firmantes = new List<Firmante>();
        if (incluir) firmantes.Add(CatalogoGerentes.RepresentanteLegal);
        var gerentes = regla.Tipo == TipoPlantilla.SeminuevosContado ? CatalogoGerentes.Seminuevos : CatalogoGerentes.Nuevos;
        firmantes.Add(gerentes.GetValueOrDefault(regla.Agencia) ?? new("", "", "", TipoFirmante.GerenteDeVentas));
        firmantes.Add(new(PreparadorVariables.Texto(datos, "vendedor"), PreparadorVariables.Texto(datos, "email_vendedor"), PreparadorVariables.Texto(datos, "num_tel_vendedor"), TipoFirmante.Apv));
        firmantes.Add(new(PreparadorVariables.Texto(datos, "nombre_completo"), PreparadorVariables.Texto(datos, "email"), PreparadorVariables.Texto(datos, "celular"), TipoFirmante.Cliente));
        return firmantes;
    }
}
