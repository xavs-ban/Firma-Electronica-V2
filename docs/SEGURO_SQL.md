# Seguro capturado y persistencia SQL

La captura de seguro de `mi_api/form.js` se conserva en `segurosPorReferencia` y se utiliza al preparar las variables Legalario. Ese comportamiento está implementado en `PreparadorVariables`: aseguradora, póliza, vigencia y conectividad según el tipo de venta y el indicador de seguro.

El endpoint antiguo `/api/actualizar-seguro` de `mi_api/index.js` no aparece invocado por el formulario. Su SQL referencia `FTVENBI_PR.Folio_control` sin relacionar `FTVENBI_PR` y actualiza `FTSEGBI_PR.CONECTIVIDAD`.

El 8 de septiembre de 2026 se consultó `INFORMATION_SCHEMA.COLUMNS` de la conexión actual, sin modificar registros:

- `FTSEGBI_PR` no contiene `CONECTIVIDAD`.
- `FTVENBI_PR` no contiene `Folio_control`.
- Las tablas contienen campos como `REFX`, `REFERENCIA` y `NRO_CONTRATO`, pero su existencia no demuestra una relación única ni el destino de conectividad.

No se implementó una escritura basada en una unión supuesta. Si se desea persistir la captura además de incorporarla al documento, se debe definir la tabla o procedimiento de destino y verificar la unicidad del registro a actualizar. No se ejecutó el UPDATE antiguo ni se cambió el esquema.
