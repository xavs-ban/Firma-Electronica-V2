# Mapa funcional

La version nueva debe conservar las funcionalidades actuales de la plataforma.

## Funcionalidades obligatorias

- Login y sesion de usuario.
- Seleccion de agencia.
- Captura de referencias a firmar.
- Generacion de documentos desde plantillas Legalario.
- Reglas actuales por agencia, tipo de expediente, tipo de venta, seminuevos/persona moral y Hyundai.
- Consumo de variables provenientes de stored procedures.
- Consulta de Mis documentos.
- Busqueda, filtros, paginacion y orden por documentos recientes.
- Visualizacion de PDF generado.
- Convocatoria de firmas.
- Eliminacion de documentos.
- Consulta de estados de firma con formato personas firmadas / personas convocadas.
- Manejo de gerentes, APV, cliente, representante legal y gerente de ventas segun reglas vigentes.
- Mensajes claros cuando Legalario todavia prepara el documento o cuando el repositorio no tiene el archivo disponible.

## Restricciones

- Las reglas actuales de Nissan e Hyundai se migran tal como estan funcionando hoy.
- El selector visible de tipo de venta alimenta variables de plantilla; no decide por si solo la plantilla.
- La plantilla se decide con datos del SP cuando aplique la regla actual.
