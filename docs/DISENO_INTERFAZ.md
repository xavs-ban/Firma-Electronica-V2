# Propuesta de rediseno de interfaz

La nueva interfaz debe sentirse mas moderna y amable, pero sin cambiar de golpe la forma de trabajo de los usuarios.

## Direccion visual

- Estilo tipo iOS: superficies limpias, botones claros, estados visibles y espaciado mas cuidado.
- Mantener el flujo actual: captura de referencias, generar, Mis documentos, ver PDF y convocar firma.
- Reducir ruido visual: menos cajas pesadas, tablas mas legibles y acciones agrupadas.
- Usar mensajes humanos cuando Legalario tarde o falle, sin ocultar el error tecnico cuando sirva para soporte.

## Pantalla principal

- Encabezado con logo, nombre de usuario y boton de salida.
- Panel de captura de referencias con campos mas compactos.
- Boton principal de generar documentacion claramente destacado.
- Resultado por referencia con estado: creado, error, preparando, listo para convocar.

## Mis documentos

- Filtros arriba: agencia, segmento, tipo de plantilla y busqueda.
- Tabla paginada desde el inicio, ordenada por fecha de creacion descendente.
- Estado como texto `firmados/convocados`, sin depender del porcentaje visual.
- Acciones con iconos: ver, convocar/reenviar, eliminar.

## Convocar firma

- Modal mas limpio con encabezado del documento.
- Firmantes en filas faciles de revisar.
- Tipo de firmante visible como etiqueta no editable cuando venga de regla.
- Mensajes separados: validando repositorio, enviando invitaciones, exito, error recuperable.

## Pendientes de decision

- Confirmar si el logo de Hyundai/Nissan cambia por agencia o se mantiene el encabezado actual.
- Confirmar si los usuarios quieren modo compacto para pantallas chicas/laptops.
- Confirmar si se mantiene Bootstrap o si se construye CSS propio ligero en Razor Pages.
