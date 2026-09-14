using FirmaElectronica.Domain.Agencias;
using FirmaElectronica.Domain.Plantillas;
namespace FirmaElectronica.Application.Services;
public static class PlantillasPorAgencia
{
    public static IReadOnlyDictionary<TipoPlantilla, string> Obtener(string agencia)
    {
        agencia = agencia.Trim().ToUpperInvariant();
        if (!CatalogoAgencias.Todas.ContainsKey(agencia)) throw new ArgumentException("Agencia desconocida.");
        var resultado = new Dictionary<TipoPlantilla, string>
        {
            [TipoPlantilla.PersonaMoral] = CatalogoPlantillas.PersonaMoral.GetValueOrDefault(agencia, CatalogoPlantillas.PersonaMoral["457"]),
            [TipoPlantilla.SeminuevosContado] = CatalogoPlantillas.SeminuevosContado.GetValueOrDefault(agencia, CatalogoPlantillas.SeminuevosContado["306"])
        };
        if (CatalogoAgencias.EsHyundai(agencia)) resultado[TipoPlantilla.Hyundai] = CatalogoPlantillas.Hyundai[agencia];
        else
        {
            resultado[TipoPlantilla.Contado] = CatalogoPlantillas.Contado.GetValueOrDefault(agencia, CatalogoPlantillas.Contado["306"]);
            resultado[TipoPlantilla.Financiamiento] = CatalogoPlantillas.Financiamiento.GetValueOrDefault(agencia, CatalogoPlantillas.Financiamiento["306"]);
        }
        return resultado;
    }
}
