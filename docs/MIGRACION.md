# Plan de migracion

## Meta

Tener una version migrada y validada durante septiembre, priorizando estabilidad operativa y compatibilidad con el flujo actual.

## Fase 1: Base tecnica

- Crear solucion .NET con capas separadas.
- Documentar alcance funcional completo.
- Preparar configuracion por ambiente.
- Crear contratos internos para Legalario y stored procedures.

## Fase 2: Inventario de la plataforma actual

- Extraer mapeo de agencias.
- Extraer reglas de plantillas.
- Extraer reglas de firmantes.
- Extraer mapeo de variables SP -> Legalario.
- Documentar payloads actuales de creacion y convocatoria.

## Fase 3: Backend nuevo

- Implementar cliente Legalario con timeouts, reintentos controlados y errores claros.
- Implementar servicios para generar documentos.
- Implementar validacion del documento antes de convocar firmas.
- Implementar consulta paginada de documentos.
- Agregar logs para payloads y respuestas sin exponer secretos.

## Fase 4: Front nuevo

- Redisenar interfaz tipo iOS.
- Crear flujo de generacion.
- Crear Mis documentos con filtros, paginacion y estados.
- Crear convocatoria de firmas con mensajes claros.

## Fase 5: Validacion y salida

- Probar Nissan sin cambiar comportamiento.
- Probar Hyundai sin cambiar comportamiento.
- Probar errores frecuentes de Legalario.
- Preparar despliegue dev/prod en AWS.
