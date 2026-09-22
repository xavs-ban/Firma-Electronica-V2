# Actualizacion de contacto en Quiter

Antes de convocar a firma, la plataforma actual actualiza los datos de contacto del cliente en Quiter cuando existe `cta_cliente`.

## Momento del flujo

1. El usuario abre la convocatoria de firma.
2. La plataforma ubica la fila del firmante con tipo `CLIENTE`.
3. Toma correo y telefono capturados/confirmados en la pantalla.
4. Busca `cta_cliente` en los datos de la referencia.
5. Si existe cuenta de cliente, obtiene token de Quiter.
6. Envia actualizacion a Quiter.
7. Si Quiter falla, la convocatoria continúa y el resultado muestra el motivo controlado del fallo.

## Payload actual

```json
{
  "validated": true,
  "email": "cliente@correo.com",
  "phoneNumbers": ["5512345678"],
  "mobilePhoneNumber": ["5512345678"]
}
```

## Reglas importantes

- El telefono se limpia y se limita a 10 digitos.
- No se mandan arreglos vacios.
- Si no hay correo ni telefono, no se llama a Quiter.
- Se distinguen los errores de autorización y actualización por código HTTP, sin mostrar cuerpos remotos ni credenciales.
- Autorización y actualización comparten un plazo de 60 segundos; cada petición dispone de hasta 45 segundos.
- El formato se restauró a partir de `mi_api/form.js` del ZIP anterior: ambos arreglos contienen cadenas, no objetos.
