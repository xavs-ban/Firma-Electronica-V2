# Actualizacion de contacto en Quiter

Antes de convocar a firma, la plataforma actual actualiza los datos de contacto del cliente en Quiter cuando existe `cta_cliente`.

## Momento del flujo

1. El usuario abre la convocatoria de firma.
2. La plataforma ubica la fila del firmante con tipo `CLIENTE`.
3. Toma correo y telefono capturados/confirmados en la pantalla.
4. Busca `cta_cliente` en los datos de la referencia.
5. Si existe cuenta de cliente, obtiene token de Quiter.
6. Envia actualizacion a Quiter.
7. Aunque Quiter falle, el flujo actual no detiene visualmente toda la convocatoria; el error se absorbe.

## Payload actual

```json
{
  "validated": true,
  "email": "cliente@correo.com",
  "phoneNumbers": [{ "phoneNumber": "5512345678", "observations": "ACTUALIZADO DESDE FIRMA DIGITAL" }],
  "mobilePhoneNumber": [{ "phoneNumber": "5512345678", "observations": "ACTUALIZADO DESDE FIRMA DIGITAL" }]
}
```

## Reglas importantes

- El telefono se limpia y se limita a 10 digitos.
- No se mandan arreglos vacios.
- Si no hay correo ni telefono, no se llama a Quiter.
- En la V2 debemos registrar el resultado en logs internos para diagnostico, sin bloquear de mas al usuario.
