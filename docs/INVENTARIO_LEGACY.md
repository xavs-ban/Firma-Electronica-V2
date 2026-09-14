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
| Hyundai | Sí en V2, confirmado por el usuario; el código anterior lo excluye |

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

## Actualizacion de contacto en Quiter

Antes de convocar firma se actualiza el contacto del cliente en Quiter cuando existe `cta_cliente`. Se toma la fila `CLIENTE`, se limpia el telefono a 10 digitos y se manda correo/telefono sin arreglos vacios. Ver [QUITER.md](QUITER.md).

## Manejo Legalario repositorio

La plataforma actual espera hasta 45 segundos antes de convocar firma. Durante la espera valida descarga/liga del documento. Si Legalario sigue regresando archivo no encontrado en repositorio, se avisa al usuario que puede reintentar en unos minutos o generar nuevamente el documento.

## Estado del bloque de variables y consulta

- Implementados los tres órdenes de variables, los alias Hyundai y las reglas de seguro/conectividad.
- Implementadas consulta, búsqueda, combinación y paginación de documentos.
- Ver `CONTINUIDAD.md` y `API_BACKEND.md` para pruebas y pendientes actuales.

## Contraste de integración — 9 septiembre 2026

Se contrastaron los puntos de llamada de `mi_api/login.js`, `form.js` e `index.js` con los clientes de la V2:

| Función | Plataforma actual | V2 |
| --- | --- | --- |
| Acceso Legalario | `/auth/login`, email/password; credenciales de respuesta | Corregido: credenciales obtenidas en el servidor, sin cuentas estáticas |
| Tokens | `customers` al entrar; alcance de documentos en `form.js` | Sólo `customers` para establecer sesión; se corrigió el segundo canje que bloqueaba el acceso |
| Perfil de agencia | `/api/login` consulta `USUARIOS_LEGALARIO` y BCrypt | SQL sólo consulta perfil; Legalario valida contraseña por indicación del usuario |
| Generación | Trabajo local, POST `/v2/documents` | Trabajo en segundo plano y registro persistente de intentos |
| Documentos | GET `/v2/documents`, filtro template_id | Mismo recurso, paginación y filtro autorizado |
| PDF | Detalle y `/v2/documents/download` | Mismos recursos, descarga PDF autenticada |
| Convocatoria | POST `/v2/signers`, antes actualización Quiter | Mismo recurso y orden de actualización |
| Seguimiento | GET `/v2/signers?document_id=...` | Mismo recurso |
| Reenvío | POST `/v2/signers/{id}/invite` | Mismo recurso |
| Eliminación | DELETE `/v2/documents/{id}` | Mismo recurso con validación de agencia |

Este contraste de código no sustituye la validación real del proveedor. No se enviaron credenciales ni invitaciones reales durante esta revisión. Los tokens se mantienen en sesión de servidor; el navegador no recibe client_secret. La renovación automática del token en una sesión activa aún no reproduce la de `form.js`: por ahora se requiere volver a iniciar sesión si expira la autorización del proveedor.
