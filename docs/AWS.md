# Preparación para AWS EC2

Esta guía prepara una instancia Linux con systemd y Nginx local. No crea recursos AWS ni despliega automáticamente. Antes de ejecutar en producción, definir dominio, distribución Linux, arquitectura, acceso SSH/SSM y conectividad privada hacia SQL Server y Quiter.

## Clonar y validar

Instalar Git y el SDK .NET indicado en `global.json` (10.0.400). En el servidor de ejecución se necesita ASP.NET Core Runtime 10, con `/usr/bin/dotnet` disponible. Confirmar con `dotnet --info`.

```bash
git clone https://github.com/xavs-ban/Firma-Electronica-V2.git
cd Firma-Electronica-V2
bash deploy/publish.sh
```

El resultado se genera en `artifacts/publish`. El script ejecuta las pruebas y verifica que no se hayan incluido configuración privada ni expedientes locales.

## Primera instalación

Instalar Nginx siguiendo las instrucciones de la distribución. Preparar el usuario del servicio y los directorios:

```bash
sudo useradd --system --home /var/lib/firma --shell /usr/sbin/nologin firma
sudo install -d -o firma -g firma -m 700 /var/lib/firma /var/lib/firma/intentos /var/lib/firma/keys
sudo install -d -m 755 /opt/firma/releases
sudo install -d -m 700 /etc/firma
sudo install -m 600 deploy/firma.env.example /etc/firma/firma.env
sudoedit /etc/firma/firma.env
```

Completar los valores REEMPLAZAR, host SQL y dominio; nunca subir este archivo a Git. Legalario autentica a cada usuario por su API. SQL aporta el perfil, roles, agencias y referencias; no valida la contraseña del login. Conservar certificados válidos para SQL y HTTPS.

Copiar `artifacts/publish/` a un directorio nuevo `/opt/firma/releases/IDENTIFICADOR/` y crear el enlace `/opt/firma/current` hacia esa versión. Los archivos publicados deben ser legibles por `firma`. No copiar configuraciones locales del equipo de desarrollo.

```bash
sudo install -m 644 deploy/firma.service /etc/systemd/system/firma.service
sudo systemctl daemon-reload
sudo systemctl enable --now firma
sudo systemctl status firma
```

Completar dominio y rutas de certificados en `deploy/nginx.conf.example`. Obtener un certificado TLS para ese dominio antes de activar el bloque 443. Instalar la configuración en el directorio de sitios de Nginx correspondiente a la distribución, ejecutar `sudo nginx -t` y recargar Nginx. La aplicación sólo escucha en 127.0.0.1:5022. Confía exclusivamente en el proxy local; no exponer ese puerto en el Security Group.

Security Group: permitir 443 a los usuarios previstos, 80 si es necesario para redirección/emisión del certificado, y SSH sólo desde las IP autorizadas (o administrar por SSM). Habilitar salida hacia Legalario/Quiter y la ruta autorizada a SQL; no publicar SQL a Internet. No se necesita acceso entrante público al puerto 5022.

## Validación

Abrir `https://DOMINIO/`, comprobar el logo y el login, consultar una referencia de prueba autorizada y revisar documentos. Generar y enviar invitaciones sólo a destinatarios de prueba acordados. Ver logs con `sudo journalctl -u firma -n 100 --no-pager`; no registrar contraseñas ni tokens.

## Datos persistentes y actualizaciones

- Una sola instancia/proceso: sesiones y cola de trabajos viven en memoria. No escalar a múltiples instancias sin migrar sesiones, cola y almacenamiento compartido.
- `/var/lib/firma/intentos` conserva asociaciones, intentos y protección contra duplicados. Respaldar y restaurar con acceso restringido; no borrarlo al actualizar.
- `/var/lib/firma/keys` conserva claves de protección de cookies. Respaldarlas de forma cifrada junto con la configuración. Los permisos del servicio restringen su lectura.
- Reiniciar termina las sesiones en memoria; el usuario deberá volver a entrar. Esperar a que terminen las generaciones antes de actualizar: la cola pendiente no sobrevive al reinicio.
- Para actualizar: `git pull --ff-only`, ejecutar `bash deploy/publish.sh`, copiar a un nuevo directorio de releases, cambiar el enlace `current` y reiniciar `firma`. Conservar el release anterior para volver a él si la validación falla, sin borrar los datos persistentes.
- Los intentos generados localmente no están en Git. Si deben seguir disponibles tras migrar, transferir su carpeta `App_Data/intentos` de forma privada al almacenamiento de la instancia antes de habilitar el tráfico.

Referencias: [ASP.NET Core con Nginx y systemd](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0), [instalación de .NET en Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux).
