using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Agencias;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Plantillas;

namespace FirmaElectronica.Application.Services;

public sealed class PlantillaResolver : IPlantillaResolver
{
    private static readonly HashSet<string> TiposContado = ["1", "1F", "1FO", "2", "2C", "3", "7FO"];
    private static readonly HashSet<string> TiposFinanciamiento = ["1C", "1S"];

    public ReglaPlantilla Resolver(SolicitudDocumento solicitud, IReadOnlyDictionary<string, object?> datosReferencia)
    {
        var agencia = solicitud.Agencia.Trim().ToUpperInvariant();

        if (EsVerdadero(datosReferencia, "isPersonaMoral"))
        {
            var id = Buscar(CatalogoPlantillas.PersonaMoral, agencia, "457");
            return new ReglaPlantilla(agencia, TipoPlantilla.PersonaMoral, id, IncluyeRepresentanteLegal: true);
        }

        if (EsVerdadero(datosReferencia, "isSeminuevo"))
        {
            var id = Buscar(CatalogoPlantillas.SeminuevosContado, agencia, "306");
            return new ReglaPlantilla(agencia, TipoPlantilla.SeminuevosContado, id, IncluyeRepresentanteLegal: true);
        }

        if (CatalogoAgencias.EsHyundai(agencia))
        {
            var id = Buscar(CatalogoPlantillas.Hyundai, agencia, "B20ABMS009");
            return new ReglaPlantilla(agencia, TipoPlantilla.Hyundai, id, IncluyeRepresentanteLegal: true);
        }

        var tipoVentaSp = ObtenerString(datosReferencia, "tipo_de_venta");
        var tipo = ResolverTipoDesdeSp(tipoVentaSp);

        if (tipo == TipoPlantilla.Financiamiento)
        {
            var id = Buscar(CatalogoPlantillas.Financiamiento, agencia, "306");
            return new ReglaPlantilla(agencia, tipo, id, IncluyeRepresentanteLegal: false);
        }

        return new ReglaPlantilla(agencia, TipoPlantilla.Contado, Buscar(CatalogoPlantillas.Contado, agencia, "306"), IncluyeRepresentanteLegal: true);
    }

    public static TipoPlantilla ResolverTipoDesdeSp(string? tipoVentaSp)
    {
        var tipo = (tipoVentaSp ?? string.Empty).Trim().ToUpperInvariant();

        if (TiposContado.Contains(tipo))
        {
            return TipoPlantilla.Contado;
        }

        if (TiposFinanciamiento.Contains(tipo))
        {
            return TipoPlantilla.Financiamiento;
        }

        return TipoPlantilla.Contado;
    }

    private static string Buscar(IReadOnlyDictionary<string, string> catalogo, string agencia, string agenciaDefault) =>
        catalogo.TryGetValue(agencia, out var templateId) ? templateId : catalogo[agenciaDefault];

    private static bool EsVerdadero(IReadOnlyDictionary<string, object?> datos, string key) =>
        datos.TryGetValue(key, out var value) && value is bool flag && flag;

    private static string ObtenerString(IReadOnlyDictionary<string, object?> datos, string key) =>
        datos.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
}
