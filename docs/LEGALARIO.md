# Creación de documentos en Legalario

## Implementación disponible

`ICreadorDocumentoLegalario` recibe un `DocumentoParaCrear` con referencia, nombre, identificador de plantilla y variables ya preparadas. El token se recibe por llamada: pertenece a la sesión del usuario y no se guarda en opciones ni en encabezados compartidos.

`CreadorDocumentoLegalario` envía un único POST a `/v2/documents`. La estructura se contrastó con `mi_api/form.js` y `mi_api/index.js`:

- `name`: nombre preparado por la aplicación.
- `type`: `template`.
- `template_id`: plantilla resuelta antes del envío.
- `sequence`: arreglo de arreglos con un objeto `key`/`value` por posición.

Las posiciones se ordenan y deben ser consecutivas desde 1. Los valores vacíos se conservan para no desplazar variables. Una respuesta válida debe contener `data.id` como texto no vacío. `DocumentoGenerado.CreadoEn` registra el inicio local de la solicitud, no una fecha certificada por Legalario.

## Configuración

El registro de servicios de Web usa `Legalario:BaseUrl` (por defecto `https://api.legalario.com`) y `Legalario:TimeoutSeconds` (por defecto 120). La URL es la raíz del servicio, sin `/v2`, parámetros ni credenciales.

El cliente HTTP dedicado tiene su timeout general desactivado; el cliente de creación aplica el límite configurado. Las redirecciones automáticas están desactivadas. No agregar políticas automáticas de reintento a este cliente.

## Resultado incierto

Un corte de red, interrupción de lectura, timeout, cancelación durante el envío, error 5xx, HTTP 408, redirección o respuesta exitosa sin identificador produce `CreacionDocumentoException` con `ResultadoIncierto = true`. El documento podría haberse creado. Los errores HTTP 4xx restantes se reportan como rechazo, conservando el código HTTP. Ningún caso se reintenta automáticamente.

Una cancelación previa al envío mantiene `OperationCanceledException` y no realiza la petición. Durante el envío se considera incierta porque cancelar la espera local no revierte la operación remota.

Los mensajes de error no incluyen cuerpos de respuesta, tokens ni excepciones internas del transporte.

## Flujo conectado

La preparación de variables, sesiones, consulta, PDF, convocatoria, reenvío, eliminación y Quiter ya tienen servicios y rutas. El registro de intentos evita repetir una creación incierta y la cola de trabajos permite consultar el avance sin mantener la petición inicial abierta. Ver `API_BACKEND.md` para contratos y configuración.

Las verificaciones usan transporte simulado. Se consultó una referencia real por SQL y se prepararon sus variables en memoria; no se crearon documentos reales ni se enviaron invitaciones. Antes de habilitar uso operativo falta configurar cuentas y validar el recorrido con servicios reales y la interfaz.
