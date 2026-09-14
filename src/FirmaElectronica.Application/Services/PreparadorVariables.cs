using System.Globalization;
using System.Text.RegularExpressions;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Plantillas;
using FirmaElectronica.Domain.Variables;

namespace FirmaElectronica.Application.Services;

public sealed class PreparadorVariables
{
    private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("en-US");
    private static readonly HashSet<string> Importes = ["precio_base", "precio_accesorios", "total_factura", "sub_total", "iva", "tasacion_vo", "km_recorridos", "valor_unidad_toma"];
    private static readonly Dictionary<string, string> Aseguradoras = new()
    {
        ["3917"] = "ALLIANZ MEXICO SA COMPAÑÍA DE SEGUROS", ["22883"] = "CHUBB SEGUROS MEXICO",
        ["2122"] = "GRUPO NACIONAL PROVINCIAL", ["6"] = "NISSAN MEXICANA", ["2124"] = "QUALITAS COMPAÑÍA DE SEGUROS",
        ["3115"] = "SEGUROS BANORTE SA DE CV GRUPO FINANCIERO BANORTE", ["2123"] = "ZURICH ASEGURADORA MEXICANA"
    };

    public DocumentoParaCrear Preparar(SolicitudDocumento solicitud, ReglaPlantilla regla,
        IReadOnlyDictionary<string, object?> datos, DatosCapturados captura, DateTimeOffset ahora)
    {
        var moral = regla.Tipo == TipoPlantilla.PersonaMoral;
        var orden = (moral ? CatalogoVariables.PersonaMoral : regla.Tipo == TipoPlantilla.Hyundai
            ? CatalogoVariables.HyundaiPersonaFisica : CatalogoVariables.PersonaFisica).ToArray();
        if (regla.Tipo == TipoPlantilla.SeminuevosContado)
        {
            var capacidad = Array.IndexOf(orden, "capacidad");
            var fecha = Array.IndexOf(orden, "fecha_de_entrega");
            (orden[capacidad], orden[fecha]) = (orden[fecha], orden[capacidad]);
        }
        if (regla.Tipo == TipoPlantilla.Financiamiento)
            orden = [.. orden.Take(104), "pais_nacimiento", "numero_exterior", "nacional", "extranjero"];
        var local = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(ahora, "America/Mexico_City");
        var fechaPlanta = captura.FechaPlanta ?? Fecha(Texto(datos, "fecha_reporte_planta"));
        var accesorios = Texto(datos, "accesorios").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var variables = new Dictionary<int, string>();
        var seguro = ResolverSeguro(solicitud, datos, captura.Seguro);
        for (var i = 0; i < orden.Length; i++)
        {
            var nombre = orden[i];
            var clave = CatalogoVariables.AliasHyundai.GetValueOrDefault(nombre, nombre);
            var candidatos = new[] { clave, nombre }.Concat(CatalogoVariables.FallbacksHyundai.GetValueOrDefault(clave, []));
            var valor = candidatos.Select(c => Texto(datos, c)).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
            if (moral && string.IsNullOrWhiteSpace(valor) && CatalogoVariables.FallbacksRepresentanteLegal.TryGetValue(clave, out var respaldo))
                valor = Texto(datos, respaldo);
            if (moral && clave == "nombre_completo_rl" && string.IsNullOrWhiteSpace(valor))
                valor = string.Join(' ', new[] { "nombre_ap", "apaterno_ap", "amaterno_ap" }.Select(c => Texto(datos, c)).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (clave.StartsWith("accesorio_") && int.TryParse(clave[10..], out var indice))
                valor = indice > 0 && indice <= accesorios.Length ? accesorios[indice - 1] : "";
            if (seguro.TryGetValue(clave, out var valorSeguro)) valor = valorSeguro;
            if (Importes.Contains(clave)) valor = Numero(Texto(datos, clave)).ToString("N2", Formato);
            valor = clave switch
            {
                "dia_planta" => fechaPlanta?.ToString("dd") ?? "",
                "mes_planta" => fechaPlanta?.ToString("MM") ?? "",
                "año_planta" => fechaPlanta?.ToString("yyyy") ?? "",
                "dia_hoy" => local.ToString("dd"), "mes_hoy" => local.ToString("MM"), "año_hoy" => local.ToString("yyyy"),
                "hora_actual" => local.ToString("HH:mm:ss"), "Folio_control" => captura.FolioControl,
                "tipo_venta" => solicitud.TipoVentaSeleccionado,
                "dinero_letra" => ImporteEnLetras.Convertir(Numero(Texto(datos, "total_factura"))),
                "hora_entrega" => Hora(Texto(datos, "hora_entrega")),
                _ => valor
            };
            variables[i + 1] = valor;
        }
        var nombreCompleto = Texto(datos, "nombre_completo");
        var vin = Texto(datos, "vin");
        return new(solicitud.Referencia, $"Documentación_{(string.IsNullOrEmpty(nombreCompleto) ? "Sin Nombre" : nombreCompleto)}_{(string.IsNullOrEmpty(vin) ? "Sin VIN" : vin)}", regla.LegalarioTemplateId, variables) { PosicionHoraActual = Array.IndexOf(orden, "hora_actual") + 1 };
    }

    public static string Texto(IReadOnlyDictionary<string, object?> datos, string clave) =>
        datos.TryGetValue(clave, out var valor) ? Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "" : "";
    private static decimal Numero(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return 0;
        if (!decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) || numero < 0)
            throw new ArgumentException("La referencia contiene un importe inválido.");
        return numero;
    }
    private static DateOnly? Fecha(string valor) => string.IsNullOrWhiteSpace(valor) ? null :
        DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) ? DateOnly.FromDateTime(fecha) :
        throw new ArgumentException("La referencia contiene una fecha inválida.");
    private static string Hora(string valor)
    {
        var coincidencia = Regex.Match(valor, @"(\d{1,2}):(\d{2})(AM|PM)", RegexOptions.IgnoreCase);
        return coincidencia.Success ? $"{coincidencia.Groups[1].Value.PadLeft(2, '0')}:{coincidencia.Groups[2].Value} {coincidencia.Groups[3].Value.ToUpperInvariant()}" : "";
    }
    private static string Vigencia(DateOnly? inicio, DateOnly? fin)
    {
        if (inicio is null || fin is null) return "";
        if (fin < inicio) throw new ArgumentException("La vigencia del seguro termina antes de iniciar.");
        var meses = (fin.Value.Year - inicio.Value.Year) * 12 + fin.Value.Month - inicio.Value.Month;
        return $"{meses} mes(es) ({meses / 12} año(s))";
    }
    private static Dictionary<string, string> ResolverSeguro(SolicitudDocumento solicitud, IReadOnlyDictionary<string, object?> datos, SeguroCapturado? captura)
    {
        var contado = new[] { "CON", "TUA", "OTR", "SIC" }.Contains(solicitud.TipoVentaSeleccionado);
        var resultado = new Dictionary<string, string> { ["Nombre_seguro"] = "", ["poliza"] = "", ["vigencia"] = "", ["conectividad"] = "" };
        if (contado && (!solicitud.AplicaSeguro || captura is null))
        {
            resultado["Nombre_seguro"] = "No compró seguro";
            resultado["conectividad"] = captura?.Conectividad ?? (!solicitud.AplicaSeguro ? Texto(datos, "conectividad") : "");
        }
        else if (captura is not null)
        {
            resultado["Nombre_seguro"] = contado && string.IsNullOrWhiteSpace(captura.Aseguradora) ? "No compró seguro" : captura.Aseguradora ?? "";
            resultado["poliza"] = captura.Poliza ?? "";
            resultado["vigencia"] = Vigencia(captura.Inicio, captura.Fin);
            resultado["conectividad"] = captura.Conectividad ?? "";
        }
        else if (!string.IsNullOrWhiteSpace(Texto(datos, "poliza")))
        {
            resultado["Nombre_seguro"] = Aseguradoras.GetValueOrDefault(Texto(datos, "cod_aseguradora"), "Desconocido");
            resultado["poliza"] = Texto(datos, "poliza");
            resultado["vigencia"] = Vigencia(Fecha(Texto(datos, "fecha_inicio_seguro")), Fecha(Texto(datos, "fecha_fin_seguro")));
            resultado["conectividad"] = "Conectividad";
        }
        return resultado;
    }
}
