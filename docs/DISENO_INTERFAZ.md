# Interfaz implementada

La V2 utiliza Razor Pages, CSS propio y módulos JavaScript sin dependencias externas de interfaz. Mantiene el recorrido operativo con superficies claras, navegación lateral y adaptación móvil.

## Pantallas

- Acceso con sesión real, errores y salida.
- Generar documentos: agencia, consulta de referencias, datos del cliente y vehículo, folio, fecha de planta, seguro y conectividad; generación en segundo plano.
- Mis documentos: búsqueda, filtro de plantilla, paginación, PDF, firmas, convocatoria, reenvío y eliminación con confirmación.
- Actividad: avance por referencia, recuperación de intentos y autorización explícita de nueva versión.
- Firmantes: contactos editables y roles definidos por el backend, incluido representante legal en Hyundai.

## Prueba local

Abrir http://localhost:5022 y seleccionar «Explorar con datos de ejemplo». Se puede consultar la referencia 900001. El adaptador de ejemplo sólo modifica memoria del navegador: no crea documentos reales, no envía invitaciones y no actualiza Quiter. Está ofrecido únicamente cuando el servidor está en Development. Al recargar se reinicia el ejemplo.

El acceso real requiere configurar la V2 según API_BACKEND.md. La simulación permite revisar la interfaz, pero no valida las integraciones externas.

## Verificación

Se revisaron consulta y generación simuladas, actividad, listado, preparación de cuatro firmantes y convocatoria simulada con resultado 0 de 4. Se inspeccionó el diseño a 390 píxeles. El visor PDF depende del soporte del navegador; se ofrece descarga y una indicación cuando la vista previa no aparece. El navegador integrado usado para esta revisión no renderizó el PDF.

Pendiente: prueba integral con cuentas y documentos designados en los servicios reales, y revisión operativa con usuarios.

## Folio de control

El formulario inicial permite capturar folio para nuevos y conserva el valor en la tarjeta al consultar. Para nuevos se exige antes de generar. El tipo de expediente lo determina la consulta: seminuevos oculta y deshabilita la captura adicional y envía folio vacío, fecha nula y seguro sin sobreescritura. Basta consultar la referencia y pulsar Generar. En el ejemplo, 900003 representa un seminuevo.

## Ajustes por revisión del usuario

Se recuperó Montserrat, azul #132B81, turquesa #17A2B8 y encabezados de modal con color. Seguro y conectividad se editan en un modal con guardar/cancelar. Conectividad conserva los valores completos: No aplica, No se adquirió servicio, Nissan Connect Finder y Nissan Connect Services; datos previos se preservan hasta editar.

El visor intenta la liga del detalle o format=URL antes de descargar bytes PDF, como la plataforma actual. Las ligas HTTPS se abren en el navegador sin reenviar el token. Se verificó con transporte simulado; falta confirmar el documento real reportado por el usuario. 94 pruebas correctas. El guardado y reapertura del modal se verificó en el navegador con datos de ejemplo.

## Revisión de captura y retroalimentación

Se limita la preparación a un expediente por vez; se conserva el flujo consultar, revisar y generar. Las referencias no encontradas muestran orientación para revisar captura o esperar registro. Los avisos usan un diálogo con Entendido. Seguro y conectividad permanecen visibles en un formulario único sin interruptor de captura. La navegación describe Nuevo expediente, Mis documentos y Actividad; Actualizar repite la consulta actual. Actividad conserva seguimiento de la sesión del navegador, no un historial completo; Revisar generación permite conciliar antes de autorizar otra versión. Se aumentó la tipografía y se retiraron las líneas azul y turquesa.
