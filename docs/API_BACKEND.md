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

Si el servidor se reinicia, consultar el intento por referencia y plantilla. Un trabajo que aún no inició puede no tener intento; una operación que inició deja su registro antes del POST remoto. Un registro `EnCurso` o `Incierto` bloquea otro envío. La conciliación compara nombre, plantilla consultada y fecha; presenta candidatos y requiere seleccionar explícitamente un documento. Esa selección debe hacerse después de revisar el PDF, porque nombre y fecha por sí solos no prueban que todas las variables coincidan.

La nueva generación sólo se autoriza desde un estado confirmado, conciliado o rechazado. Para un documento confirmado debe enviarse su identificador anterior; para un rechazo sin documento se envía `null`. Un `OperacionId` diferente no autoriza por sí solo una nueva generación: con los mismos datos se reutiliza el documento existente y con datos distintos se exige el endpoint de autorización. Los cambios de estado conservan historial. La hora actual se excluye de la comparación de contenido para que un segundo clic no genere otro documento sólo por haber cambiado el reloj.

El almacenamiento en archivos no es una cola distribuida. Para varias instancias AWS se necesita un registro compartido con control de concurrencia y una cola durable; no desplegar este modo con discos independientes. Los registros contienen nombres de documentos y deben almacenarse en un volumen privado con respaldo y política de retención.

## Consulta y errores

La lista combina páginas y plantillas, elimina identificadores repetidos y ordena globalmente por fecha. Para evitar devolver datos parciales se rechazan consultas de más de 500 páginas por plantilla. Antes de escalar el volumen conviene sustituir esta lectura completa por un índice local o una API remota con orden garantizado.

Los errores API devuelven `mensaje`, `resultadoIncierto` y `reintentable`. Los códigos principales son 400 validación/CSRF, 401 sesión ausente, 403 permisos, 404 no encontrado, 409 conflicto de operación y 502 respuesta remota no confirmada y 503 espera temporal segura. Sólo se permite el reintento automático cuando `reintentable` es verdadero y `resultadoIncierto` es falso; nunca por un timeout, desconexión o 5xx del POST remoto.

La convocatoria consulta el endpoint de firmantes durante un máximo de 45 segundos, reintentando lecturas temporales cada 3 segundos. La disponibilidad de una liga o PDF no se usa como condición para convocar. Si ya existen firmantes, registra `Conciliado` y devuelve `recuperada: true` sin volver a enviar. Si Quiter falla continúa con aviso. Un rechazo confirmado queda como `Rechazado` o `RechazadoTemporal` y permite un nuevo intento, previa consulta de firmantes. Un timeout, error de red o 5xx al enviar queda `Incierto`; junto con los registros antiguos `EnCurso`, bloquea otro POST aunque una consulta todavía devuelva cero firmantes. La interfaz vuelve a consultar la preparación y permite como máximo un reintento automático de convocatoria ante un rechazo temporal seguro, dentro del límite total de 130 segundos. Los tokens expirados requieren renovar la sesión. No se registran cuerpos remotos ni secretos en los mensajes de error.

## Compatibilidad del flujo de firma

La convocatoria omite `workflow`, igual que el ZIP anterior proporcionado por el equipo, que documenta su retiro por recomendación del proveedor. Conserva `document_id`, `use_whatsapp`, `send_invite` y los firmantes con `fullname`, `email`, `phone`, `type` y `role`.

Dentro del modal «Reenvío de invitaciones y enlace», junto a Reenviar invitación, se permite obtener y copiar el enlace de cada firmante existente (`https://saas.legalario.com/portal/invitacion/{signerId}`). Esta acción sólo consulta; no crea firmantes ni reenvía invitaciones. La generación y el envío confirmado presentan un modal de resultado.
