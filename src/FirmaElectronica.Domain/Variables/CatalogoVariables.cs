namespace FirmaElectronica.Domain.Variables;

public static class CatalogoVariables
{
    public static readonly IReadOnlyList<string> PersonaFisica = [
        "NOMBRE", "APELLIDO1", "APELLIDO2", "pais", "aut_iden", "id_identificacion", "F_Nacimi", "Direccion", "RFC",
        "curp", "nacionalidad", "celular", "telefono", "email", "nombre_identificacion", "folio_iden", "GIRO", "puesto_de_trabajo",
        "vendedor", "nombre_completo", "cp", "colonia", "delegacion", "color", "tapiceria", "marca", "modelo", "tipo_vn",
        "version", "ANIO_VEHI", "clave_vehicular", "vin", "estado", "no_motor", "NOM_EMPRESA", "dealer", "direccion_concesionario",
        "folio", "serie", "catalogo", "capacidad", "fecha_de_entrega", "sub_total", "iva", "total_factura", "tasacion_vo",
        "vin_seminuevo", "marca_vo", "modelo_vo", "color_vo", "año_vo", "hora_entrega", "bastidor",
        "rfc_razon", "direccion_razon", "localidad", "cp_razon", "correo_razon", "tel_razon", "desc_tventa", "dia_planta", "mes_planta",
        "año_planta", "dinero_letra", "hora_entrega_prevista", "pais_empresa", "id_empresa", "Folio_control", "hora_actual", "pedido",
        "factura_nissan", "fecha_factura_nissan", "fecha_factura_cliente", "precio_base", "precio_accesorios", "tipo_venta",
        "Nombre_seguro", "poliza", "vigencia", "conectividad",
        "km_recorridos", "placas", "repuve", "submarca_toma", "repuve_toma", "valor_unidad_toma", "gerente_ventas",
        "dia_impresion", "mes_impresion", "anio_impresion", "hora_impresion", "dia_fact", "mes_fact", "anio_fact",
        "accesorio_1", "accesorio_2", "accesorio_3", "accesorio_4", "accesorio_5",
        "accesorio_6", "accesorio_7", "accesorio_8", "accesorio_9", "accesorio_10"
    ];

    public static readonly IReadOnlyList<string> HyundaiPersonaFisica = [
        "Nombre", "Apellido1", "Apellido2", "Pais", "aut_iden", "id_identificacion", "F_Nacimi", "Direccion", "RFC",
        "curp", "nacionalidad", "celular", "telefono", "email", "nombre_identificacion", "folio_iden", "GIRO", "puesto_de_trabajo",
        "vendedor", "nombre_completo", "cp", "colonia", "delegacion", "color", "tapiceria", "marca", "modelo", "tipo_vn",
        "version", "ANIO_VEHI", "clave_vehicular", "vin", "estado", "no_motor", "NOM_EMPRESA", "dealer", "direccion_concesionario",
        "folio", "serie", "catalogo", "capacidad", "fecha_de_entrga", "sub_total", "iva", "total_factura", "tasacion_vo",
        "vin_seminuevo", "marca_vo", "modelo_vo", "color_vo", "año_vo", "Hora_entrega", "bastidor",
        "rfc_razon", "direccion_razon", "localidad", "cp_razon", "correo_razon", "tel_razon", "desc_tventa", "Dia_hoy", "Mes_Hoy",
        "Año_Hoy", "Dinero_letra", "hora_entrega_prevista", "pais_empresa", "id_empresa", "Folio_control", "hora_actual", "pedido",
        "factura_nissan", "fecha_factura_nissan", "fecha_factura_cliente", "precio_base", "precio_accesorios", "tipo_venta",
        "Nombre_Seguro", "Poliza_Seguro", "Vigencia_Seguro", "Conectividad",
        "null 001", "null 004", "null 006", "null 005", "null 009", "null 010", "null 003", "null 007", "null 008",
        "null 012", "null 013", "null 014", "null", "null",
        "accesorio_03", "accesorio_04", "accesorio_05", "accesorio_06", "accesorio_07", "accesorio_08",
        "accesorio_09", "accesorio_10", "acceosrio_11", "acceosrio_12",
        "id_cliente", "estado_civil", "fecha_pedido", "tipo_ven_dest", "tipo_ped", "num_pedimento"
    ];

    public static readonly IReadOnlyDictionary<string, string> AliasHyundai = new Dictionary<string, string>
    {
        ["Nombre"] = "NOMBRE",
        ["Apellido1"] = "APELLIDO1",
        ["Apellido2"] = "APELLIDO2",
        ["Pais"] = "pais",
        ["fecha_de_entrga"] = "fecha_de_entrega",
        ["Hora_entrega"] = "hora_entrega",
        ["Dia_hoy"] = "dia_hoy",
        ["Mes_Hoy"] = "mes_hoy",
        ["Año_Hoy"] = "año_hoy",
        ["Dinero_letra"] = "dinero_letra",
        ["Nombre_Seguro"] = "Nombre_seguro",
        ["Poliza_Seguro"] = "poliza",
        ["Vigencia_Seguro"] = "vigencia",
        ["Conectividad"] = "conectividad",
        ["id_cliente"] = "cta_cliente",
        ["acceosrio_11"] = "accesorio_11",
        ["acceosrio_12"] = "accesorio_12"
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> FallbacksHyundai = new Dictionary<string, IReadOnlyList<string>>
    {
        ["cta_cliente"] = ["CTA_CLIENTE", "id_cliente"],
        ["tipo_ven_dest"] = ["TIPO_VEN_DEST", "tipo_vent_des", "TIPO_VENT_DES"],
        ["tipo_ped"] = ["TIPO_PED", "tipo_pedido", "TIPO_PEDIDO"]
    };

    public static readonly IReadOnlyList<string> PersonaMoral = [
        "pais", "aut_iden", "id_identificacion", "F_Nacimi", "Direccion", "RFC", "curp", "nacionalidad", "celular", "telefono",
        "email", "nombre_identificacion", "folio_iden", "puesto_de_trabajo", "vendedor", "nombre_completo", "cp", "colonia",
        "delegacion", "color", "tapiceria", "marca", "modelo", "tipo_vn", "version", "ANIO_VEHI", "clave_vehicular", "vin",
        "estado", "no_motor", "NOM_EMPRESA", "dealer", "direccion_concesionario", "folio", "serie", "catalogo", "capacidad",
        "fecha_de_entrega", "sub_total", "iva", "total_factura", "tasacion_vo", "vin_seminuevo", "marca_vo", "modelo_vo",
        "color_vo", "año_vo", "hora_entrega", "bastidor", "rfc_razon", "direccion_razon", "localidad", "cp_razon", "correo_razon",
        "tel_razon", "desc_tventa", "dia_hoy", "mes_hoy", "año_hoy", "dinero_letra", "pais_empresa", "id_empresa", "hora_entrega_prevista",
        "NOMBRE_COMERCIAL", "actividad", "fecha_constitucion", "fecha_inscripcion", "fecha_poder",
        "nombre_rl", "apaterno_rl", "amaterno_rl", "direccion_rl", "cp_rl", "colonia_rl", "delegacion_rl", "estado_rl",
        "curp_rl", "rfc_rl", "fecha_nacimiento_rl", "tipo_iden_rl", "institucion_rl", "telefono_rl", "tel_novil_rl",
        "email_rl", "nacionalidad_rl", "puesto_rl",
        "nombre_s", "apaterno_s", "amaterno_s", "direccion_s", "cp_s", "colonia_s", "delegacion_s", "estado_s", "curp_s",
        "rfc_s", "fecha_nacimiento_s", "tipo_iden_s", "institucion_s", "telefono_s", "tel_novil_s", "email_s", "nacionalidad_s", "puesto_s",
        "nombre_ap", "apaterno_ap", "amaterno_ap", "direccion_ap", "cp_ap", "colonia_ap", "delegacion_ap", "estado_ap",
        "curp_ap", "rfc_ap", "fecha_nacimiento_ap", "tipo_iden_ap", "institucion_ap", "telefono_ap", "tel_novil_ap",
        "email_ap", "nacionalidad_ap", "puesto_ap",
        "num_iden_rl", "num_iden_s", "num_iden_ap", "placas", "submarca", "submarca_vo", "nombre_completo_rl",
        "tipo_venta", "Nombre_seguro", "poliza", "vigencia", "conectividad", "accesorio_01", "accesorio_02",
        "accesorio_03", "accesorio_04", "accesorio_05", "accesorio_07", "accesorio_06", "accesorio_08", "accesorio_09",
        "accesorio_10", "Folio_control", "pedido", "factura_nissan", "fecha_factura_nissan", "fecha_factura_cliente",
        "tipo_venta", "precio_base", "precio_accesorios", "hora_actual"
    ];

    public static readonly IReadOnlyDictionary<string, string> FallbacksRepresentanteLegal = new Dictionary<string, string>
    {
        ["nombre_rl"] = "nombre_ap",
        ["apaterno_rl"] = "apaterno_ap",
        ["amaterno_rl"] = "amaterno_ap",
        ["direccion_rl"] = "direccion_ap",
        ["cp_rl"] = "cp_ap",
        ["colonia_rl"] = "colonia_ap",
        ["delegacion_rl"] = "delegacion_ap",
        ["estado_rl"] = "estado_ap",
        ["curp_rl"] = "curp_ap",
        ["rfc_rl"] = "rfc_ap",
        ["fecha_nacimiento_rl"] = "fecha_nacimiento_ap",
        ["tipo_iden_rl"] = "tipo_iden_ap",
        ["institucion_rl"] = "institucion_ap",
        ["telefono_rl"] = "telefono_ap",
        ["tel_novil_rl"] = "tel_novil_ap",
        ["email_rl"] = "email_ap",
        ["nacionalidad_rl"] = "nacionalidad_ap",
        ["puesto_rl"] = "puesto_ap",
        ["num_iden_rl"] = "num_iden_ap"
    };
}
