# Estado de la migración

Actualizado el 8 de septiembre de 2026. Proyecto: FirmaElectronicaV2. Último commit previo a este bloque: `32d760f`. Los cambios de este bloque están en el árbol de trabajo local.

## Acuerdos

- Migrar las funciones del backend y probarlas antes de conectar la interfaz.
- Conservar el comportamiento operativo de Nissan, Hyundai, persona moral y seminuevos.
- Documentación y nombres propios del proyecto en español; respetar los nombres exigidos por .NET, SQL y las API.
- Hyundai incluye al representante legal, confirmado por el usuario y activado por defecto.
- Rediseño visible pero conservador. Interfaz Razor con CSS propio, formularios y estados de carga.
- Actualizar el contacto del cliente en Quiter antes de convocar; un fallo de Quiter deja aviso y no cancela por sí solo la convocatoria.

## Implementado

- Acceso mediante `/auth/login` de Legalario y canje de tokens según login.js y form.js; SQL sólo aporta perfil y permisos. Sesión de servidor, salida y protección CSRF.
- Consulta de referencias: persona moral, persona física y seminuevos, en ese orden. Se rechazan duplicados.
- Selección de plantilla y preparación de variables, alias, accesorios, importes, fechas, seguro y conectividad.
- Preparación de firmantes por plantilla/agencia y validación de contacto.
- Creación en cola: hasta tres intentos, cinco segundos entre fallos temporales/inciertos y 180 segundos de plazo total.
- Trabajos de generación en segundo plano y consulta de estado por usuario.
- Registro de intentos en archivos, exclusión de envíos simultáneos e historial; asociación manual retirada de la interfaz.
- Nuevas solicitudes e intentos inciertos pueden reintentarse sin conciliar; una misma operación confirmada reutiliza su resultado.
- Consulta y búsqueda de documentos, paginación y orden global por fecha, PDF, conteo de firmas, convocatoria, reenvío y eliminación.
- Cliente Quiter, incluido token y actualización de contacto sin arreglos vacíos.
- Rutas autenticadas descritas en `API_BACKEND.md`.

## Verificaciones

- 79 pruebas correctas: reglas, transporte simulado, concurrencia/persistencia y recorrido HTTP con sesión y CSRF.
- La ejecución de pruebas compila también Web; no hubo errores ni advertencias de compilación.
- Consulta real de solo lectura de la referencia `22473390` con el proveedor .NET: resolvió Financiamiento y preparó 104 variables en memoria.
- Consulta de estructura SQL completada. No se modificaron registros.
- No se crearon documentos reales ni se enviaron invitaciones ni actualizaciones Quiter.

## Pendientes antes de la prueba integral

1. Configurar la conexión y las cuentas de cada usuario en el entorno de la V2; existe `appsettings.example.json`, sin secretos. No se copiaron credenciales reales al repositorio.
2. Validar respuestas reales de Legalario y Quiter en un ambiente de pruebas con documentos y contactos designados para ello.
3. Validar la interfaz con usuarios y cuentas reales; el recorrido de ejemplo usa un adaptador aislado en memoria.
4. Configurar volumen persistente, sesiones y despliegue AWS. El almacenamiento actual de intentos sirve para una instancia o procesos que compartan el mismo volumen local; no habilitar varias instancias independientes con este almacenamiento.

## Seguro en SQL

El formulario actual conserva el seguro capturado en memoria y lo usa al preparar variables; ese flujo está migrado. El endpoint antiguo `/api/actualizar-seguro` no es llamado por `form.js` y contiene una consulta inválida. La estructura consultada no tiene `FTSEGBI_PR.CONECTIVIDAD` ni `FTVENBI_PR.Folio_control`. No se migró esa escritura como si fuera una función operativa. Si se requiere persistir el seguro, hay que definir su destino y relación reales; ver `SEGURO_SQL.md`.

## Recuperación de contexto

La tarea anterior «Continúa migración de Firma V2» falló por agotamiento de contexto. Se recuperó su historial y los archivos. El arreglo `8772156` pertenecía a la plataforma actual; su despliegue no se verificó en esta tarea.
