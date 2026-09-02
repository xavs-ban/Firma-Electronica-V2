# Inventario legacy

Este inventario sale de la plataforma actual en Node.js y sirve como base de migracion para la V2 en .NET.

## Stored procedures

Orden actual de busqueda por referencia:

1. `sp_ObtenerDatosReferenciaPM` para Persona Moral.
2. `sp_ObtenerDatosReferencia` para Persona Fisica / Nuevos.
3. `sp_ObtenerDatosReferenciaSeminuevos` para Seminuevos.

Si un SP devuelve mas de un registro, la plataforma actual corta el flujo y muestra error de validacion.

## Agencias

| Clave | Agencia | Marca |
| --- | --- | --- |
| 306 | Apizaco | Nissan |
| 306T | Nami Tlaxcala | Nissan |
| 457 | Pachuca | Nissan |
| 457E | Explanada | Nissan |
| 458 | Tulancingo | Nissan |
| 459 | Huauchinango | Nissan |
| 459T | Zacatlan | Nissan |
| 472 | Angelopolis | Nissan |
| 474 | Cholula | Nissan |
| 475 | San Manuel | Nissan |
| 527 | Tula | Nissan |
| 527T | Ixmiquilpan | Nissan |
| 528 | Zumpango | Nissan |
| 528J | Jilotepec | Nissan |
| 528T | Tizayuca | Nissan |
| B20ABMS009 | Hyundai Coacalco | Hyundai |
| B20ABPA002 | Hyundai Pachuca | Hyundai |

## Regla de plantilla

La plantilla no se decide por el selector visible de tipo de venta. La plantilla se decide con el valor del SP `tipo_de_venta`.

| Valor SP | Tipo de plantilla |
| --- | --- |
| 1, 1F, 1FO, 2, 2C, 3, 7FO | CONTADO |
| 1C, 1S | FINANCIAMIENTO |
| Otro/vacio | CONTADO |

Excepciones por expediente:

| Condicion | Plantilla |
| --- | --- |
| Persona Moral | PERSONA_MORAL_TEMPLATES |
| Seminuevos | SEMINUEVOS_CONTADO_TEMPLATES |
| Hyundai nuevos | HYUNDAI templates |

## Representante legal

Regla acordada para migracion:

| Familia de plantilla | Enviar representante legal |
| --- | --- |
| CONTADO_TEMPLATES | Si |
| FINANCIAMIENTO_TEMPLATES | No |
| SEMINUEVOS_CONTADO_TEMPLATES | Si |
| PERSONA_MORAL_TEMPLATES | Si |
| Hyundai | Si |

## Payload creacion de documento

Estructura actual hacia Legalario:

```json
{
  "name": "Documentacion_NOMBRE_COMPLETO_VIN",
  "type": "template",
  "template_id": "id de plantilla",
  "sequence": [
    [{ "key": 1, "value": "valor" }],
    [{ "key": 2, "value": "valor" }]
  ]
}
```

## Payload convocatoria de firma

Estructura actual hacia Legalario:

```json
{
  "document_id": "id documento Legalario",
  "workflow": false,
  "use_whatsapp": true,
  "send_invite": true,
  "signers": [
    {
      "fullname": "NOMBRE",
      "email": "correo@dominio.com",
      "phone": "5512345678",
      "type": "CLIENTE",
      "role": "FIRMANTE"
    }
  ]
}
```

## Manejo Legalario repositorio

La plataforma actual espera hasta 45 segundos antes de convocar firma. Durante la espera valida descarga/liga del documento. Si Legalario sigue regresando archivo no encontrado en repositorio, se avisa al usuario que puede reintentar en unos minutos o generar nuevamente el documento.

## Pendiente por migrar al siguiente bloque

- Orden completo de variables Persona Fisica.
- Orden completo de variables Hyundai.
- Orden completo de variables Persona Moral.
- Mapeo de alias Hyundai.
- Reglas de seguro/conectividad.
- Paginacion actual de Mis documentos.
