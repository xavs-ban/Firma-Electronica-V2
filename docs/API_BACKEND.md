# Backend de Firma V2

Las rutas están implementadas y probadas con un servidor HTTP de pruebas. La interfaz Razor todavía no las consume. Las pruebas no equivalen a una certificación de los contratos remotos ni a una prueba de producción.

## Configuración local

Copiar `src/FirmaElectronica.Web/appsettings.example.json` a `appsettings.Local.json` en esa misma carpeta y completar los valores privados. Este archivo local está excluido de Git. También se admiten variables de entorno con `__` como separador.

- `ConnectionStrings:Firma`: conexión SQL Server a la base utilizada por la plataforma actual.
- `Legalario:BaseUrl`: raíz HTTPS de Legalario, sin `/v2`.
- `Legalario:TimeoutSeconds`: 120 por defecto.
- Las credenciales se obtienen de `/auth/login` usando el usuario y contraseña capturados. No se configura `Legalario:Cuentas`.
- `Quiter:ClientId`, `ClientSecret` y `Code`: configuración del flujo actual de autorización Quiter.
- `Firmantes:IncluirRepresentanteHyundai`: `true` por defecto, según el acuerdo confirmado: Hyundai incluye al representante legal.
- `Intentos:Carpeta`: ruta absoluta opcional a un volumen persistente; por defecto se usa `App_Data/intentos` dentro de Web.

Ejecutar desde la raíz: `dotnet run --project src/FirmaElectronica.Web --launch-profile http`. URL local: `http://localhost:5022`.

## Sesión

1. GET `/api/sesion/csrf` obtiene un token CSRF y su cookie.
2. POST `/api/sesion` recibe `usuario` y `contrasena`, conservando la cookie y enviando `X-CSRF-TOKEN`.
3. Tras entrar, pedir nuevamente `/api/sesion/csrf` para las acciones autenticadas.
4. Conservar las cookies de sesión y autenticación; enviar el encabezado CSRF en POST y DELETE.

Legalario valida usuario y contraseña en `/auth/login`. Sus credenciales se canjean en `/auth/token` con `customers`, igual que `login.js`; no se exige el segundo canje de `form.js` para entrar. La consulta y convocatoria reutilizan el token de la sesión autenticada; no solicitan un segundo token ni conservan credenciales de cliente en sesión. SQL sólo resuelve nombre, rol y agencias en `USUARIOS_LEGALARIO` y actualiza `ULTIMO_ACCESO`; no se consulta ni valida el hash local. Un usuario sin perfil no obtiene sesión ni permisos. La sesión usa memoria de servidor y expira por inactividad. Un reinicio exige iniciar sesión nuevamente; los intentos de documentos permanecen en disco. El acceso limita intentos por dirección IP. No se guarda el token Legalario en la cookie ni en los archivos de intentos.

## Rutas

| Método | Ruta | Función |
| --- | --- | --- |
| GET | `/api/sesion` | Usuario de la sesión |
| DELETE | `/api/sesion` | Cerrar sesión |
| GET | `/api/agencias` | Agencias permitidas |
| GET | `/api/agencias/{agencia}/plantillas` | Tipos y plantillas, con los respaldos de la generación |
| GET | `/api/referencias/{referencia}?agencia=...` | Consulta de referencia autorizada |
| POST | `/api/documentos/preparar` | Preparación de nombre, plantilla y variables |
| POST | `/api/firmantes/preparar` | Firmantes sugeridos según reglas |
| POST | `/api/documentos/trabajos` | Encolar generación; responde 202 y `Location` |
| GET | `/api/documentos/trabajos/{id}` | Estado del trabajo del usuario |
| POST | `/api/documentos` | Generación síncrona para integración; la interfaz debe preferir trabajos |
| GET | `/api/documentos?agencia=...` | Lista; admite `pagina`, `tamano`, `busqueda` y `plantilla` |
| GET | `/api/documentos/{id}/pdf?agencia=...` | PDF, o 202 si sigue preparándose |
| GET | `/api/documentos/{id}/firmas?agencia=...` | Firmados, convocados y detalle |
| POST | `/api/documentos/{id}/convocar` | Validación de repositorio, Quiter y convocatoria |
| POST | `/api/documentos/{id}/firmantes/{firmanteId}/reenviar?agencia=...` | Reenviar a un firmante del documento |
| DELETE | `/api/documentos/{id}?agencia=...` | Eliminar documento autorizado |
| GET | `/api/intentos/{referencia}?agencia=...&plantilla=...` | Estado persistente del intento |
| GET | `/api/intentos/{referencia}/candidatos?agencia=...&plantilla=...` | Candidatos para conciliación |
| POST | `/api/intentos/{referencia}/confirmar?agencia=...&plantilla=...` | Asociar candidato revisado: `documentoId` |
| POST | `/api/intentos/{referencia}/nueva-generacion?agencia=...&plantilla=...` | Autorizar nuevo intento: `documentoAnterior` |

Las rutas de documentos verifican la plantilla contra las permitidas para la agencia. La convocatoria también comprueba la referencia y los tipos de firmante; no permite actualizar el contacto de otra referencia. Las plantillas compartidas entre agencias conservan esa limitación del modelo actual: antes de producción debe verificarse el alcance real de cada cuenta Legalario.

## Entrada de generación y preparación

```json
{
  "solicitud": {
    "referencia": "REFERENCIA",
    "agencia": "306",
    "tipoVentaSeleccionado": "CON",
    "fechaOperacion": "2026-09-08",
    "aplicaSeguro": false
  },
  "captura": {
    "folioControl": "FOLIO",
    "fechaPlanta": "2026-09-08",
    "seguro": null
  }
}
```

El seguro opcional contiene `aseguradora`, `poliza`, `inicio`, `fin` y `conectividad`. Las fechas usan `YYYY-MM-DD`. `fechaOperacion` se conserva en el contrato; las variables de fecha de planta se obtienen de `captura.fechaPlanta` o de la referencia y la hora se calcula para Ciudad de México.

La convocatoria recibe `referencia`, `agencia` y `firmantes`. Cada firmante contiene `nombre`, `correo`, `telefono`, `tipoFirmante` y `firmaEnTodasLasHojas`. Los valores del enumerador actual son 0 cliente, 1 APV, 2 gerente y 3 representante. `firmaEnTodasLasHojas` debe ser `false`: la plataforma actual no envía ese atributo y no se inventó un contrato remoto para habilitarlo.

## Trabajos e intentos

La cola procesa trabajos en segundo plano, con capacidad de 100 pendientes. La petición HTTP de generación no espera a Legalario. El resultado en memoria queda consultable durante al menos una hora después de completar; los tokens sólo viven en memoria durante el trabajo.

La generación realiza hasta tres intentos ante errores inciertos o HTTP 429, separados por cinco segundos, con un límite total de 180 segundos desde que comienza a procesarse el trabajo (cada llamada remota conserva su límite de 120 segundos). Un rechazo de validación termina sin repetirse automáticamente. Los estados `EnCurso` e `Incierto` se conservan como historial y ya no bloquean nuevas solicitudes.

Actividad ofrece **Reintentar generación**, sin asociación manual. Conserva la solicitud en la sesión del navegador para repetirla; para actividades antiguas sin esos datos, abre la consulta con la referencia prellenada. Un `OperacionId` nuevo permite una nueva creación. Repetir el mismo ID con los mismos datos reutiliza un resultado confirmado; un ID fallido admite otro intento. Los endpoints antiguos de conciliación se conservan por compatibilidad, pero no son requisitos del flujo. Al repetir una creación cuyo resultado fue incierto puede existir un documento adicional en Legalario.

El almacenamiento en archivos no es una cola distribuida. Para varias instancias AWS se necesita un registro compartido con control de concurrencia y una cola durable; no desplegar este modo con discos independientes. Los registros contienen nombres de documentos y deben almacenarse en un volumen privado con respaldo y política de retención.

## Consulta y errores

La lista combina páginas y plantillas, elimina identificadores repetidos y ordena globalmente por fecha. Para evitar devolver datos parciales se rechazan consultas de más de 500 páginas por plantilla. Antes de escalar el volumen conviene sustituir esta lectura completa por un índice local o una API remota con orden garantizado.

Los errores API devuelven `mensaje`, `resultadoIncierto` y `reintentable`. Se conservan validación, sesión, permisos y CSRF. El navegador sólo repite automáticamente rechazos marcados como temporales y seguros; los reintentos de mutaciones inciertas se coordinan en el servidor.

La convocatoria dispone de 180 segundos, con hasta tres intentos separados por cinco segundos. Cada intento consulta los firmantes reales, con lecturas temporales cada tres segundos durante un máximo de 90 segundos y siempre dentro del plazo global. El historial local no impide enviar. Se reenvían invitaciones a los pendientes registrados y se crean los roles faltantes; se omiten quienes ya firmaron. Los reenvíos usan los contactos ya registrados en Legalario. La respuesta incluye `reenviadas` y `nuevos`; `recuperada` indica que hubo reenvíos confirmados. Si Quiter falla continúa con aviso. No se exige una liga o PDF para convocar.

La consulta del PDF espera hasta 90 segundos, reintentando respuestas temporales y HTTP 202 sin crear otro documento. La interfaz de convocatoria espera hasta 195 segundos, incluidos los pasos de autorización, y vuelve a habilitar el botón si el resultado no se confirma. El modal muestra el resultado real de los envíos. Mis documentos conserva reenvío y enlace por firmante y permite completar una convocatoria parcial. No se ejecutan pruebas contra documentos o invitaciones reales.

## Compatibilidad del flujo de firma

La convocatoria omite `workflow`, igual que el ZIP anterior proporcionado por el equipo, que documenta su retiro por recomendación del proveedor. Conserva `document_id`, `use_whatsapp`, `send_invite` y los firmantes con `fullname`, `email`, `phone`, `type` y `role`.

Dentro del modal «Reenvío de invitaciones y enlace», junto a Reenviar invitación, se permite obtener y copiar el enlace de cada firmante existente (`https://saas.legalario.com/portal/invitacion/{signerId}`). Esta acción sólo consulta; no crea firmantes ni reenvía invitaciones. La generación y el envío confirmado presentan un modal de resultado.

## Acceso individual desde Entregas

El modo compartido temporal fue retirado. Cada sesión usa las credenciales recibidas desde Entregas o las del formulario de acceso. Una sección antigua `AccesoTemporal` en la configuración ya no puede sustituir esa cuenta; el despliegue omite dicha sección en la nueva release. Las sesiones con la marca `acceso_temporal` se invalidan y deben autenticarse de nuevo. Se conservan la validación del origen, ventana y canal de Entregas, además del perfil y los permisos de cada usuario.

## Actualización de contacto en Quiter

El cuerpo vuelve al formato del ZIP anterior: `email`, `validated: true`, `phoneNumbers: ["5512345678"]` y `mobilePhoneNumber: ["5512345678"]`. Los teléfonos son cadenas, sin objetos ni observaciones adicionales; se omiten listas vacías. La actualización dispone de 60 segundos para autorización y PUT, con hasta 45 segundos por petición dentro del plazo global de convocatoria. Un fallo distingue configuración ausente, HTTP de autorización/actualización, formato inválido, conexión y tiempo agotado. No se muestran cuerpos remotos ni secretos. Las invitaciones pueden completarse aunque Quiter falle, registrando el aviso de contacto pendiente en la consola del navegador, sin incluirlo en la ventana modal.

El 21 de septiembre se verificó la autorización de Quiter con la configuración local (HTTP 200). No se actualizaron clientes reales: el funcionamiento del PUT se validó con el formato del ZIP y pruebas de transporte simulado, pendiente de comprobación en el servidor.
