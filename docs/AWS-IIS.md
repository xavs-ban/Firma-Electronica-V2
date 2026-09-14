# Firma Digital en AWS Windows / IIS

Destino solicitado: `http://10.0.128.73:3366/firma-digital/`. Se crea el sitio **Firma Digital V2**, con una aplicación IIS **firma-digital** y pool dedicado **FirmaDigitalV2**. No sustituye el sitio de Firma Electrónica existente.

## Primera instalación

En la instancia Windows, instalar Git, SDK .NET **10.0.400** (global.json) y **ASP.NET Core Hosting Bundle 10 x64**. El bundle es necesario para el módulo de IIS incluso si ya hay otras versiones de .NET. Confirmar `dotnet --info` y que IIS tenga AspNetCoreModuleV2. Coordinar cualquier reinicio requerido por la instalación, pues hay otras plataformas.

Abrir PowerShell como administrador:

```powershell
git clone https://github.com/xavs-ban/Firma-Electronica-V2.git C:\Plataformas\Firma-Electronica-V2
New-Item -ItemType Directory -Force C:\Plataformas\secrets\FirmaDigital
Copy-Item C:\Plataformas\Firma-Electronica-V2\deploy\iis-settings.example.json C:\Plataformas\secrets\FirmaDigital\appsettings.Production.json
notepad C:\Plataformas\secrets\FirmaDigital\appsettings.Production.json
```

Completar SQL y Quiter con las credenciales operativas, mediante transferencia privada. No incluirlas en Git ni pegarlas en el chat. Proteger la carpeta `secrets` para administradores y SYSTEM. No usar el archivo de ejemplo con REEMPLAZAR.

```powershell
Copy-Item C:\Plataformas\Firma-Electronica-V2\deploy\deploy-firma-digital.ps1 C:\Plataformas\deploy-firma-digital.ps1
powershell -ExecutionPolicy Bypass -File C:\Plataformas\deploy-firma-digital.ps1
```

El script comprueba el repositorio, trae `main` con `pull --ff-only`, ejecuta pruebas, publica en un release nuevo, configura el pool y cambia la ruta física de la aplicación. Conserva el release anterior y lo restaura si falla la comprobación HTTP. La primera instalación fallida conserva los archivos para diagnóstico. No borra expedientes ni cambia otros sitios. Esperar a que no haya generaciones en curso: sesiones y cola están en memoria y el reinicio las termina.

## Red y transporte

Permitir TCP **3366** sólo desde las redes de usuarios previstas en Windows Firewall y el Security Group de AWS; el script no abre reglas de red. Esta IP es privada y requiere conectividad a la red de la instancia.

El destino solicitado usa HTTP: el script activa explícitamente `Hosting:HttpInterno` para permitir cookies sobre ese transporte y evitar la redirección a HTTPS. HTTP no cifra las credenciales entre navegador e IIS. Utilizar exclusivamente la red interna autorizada; para HTTPS, configurar certificado/binding y desactivar esa opción. No deshabilitar la validación TLS de Legalario o Quiter.

## Actualizaciones

```powershell
powershell -ExecutionPolicy Bypass -File C:\Plataformas\deploy-firma-digital.ps1
```

Si cambia el propio script en Git, actualizar primero el clon y copiar nuevamente el archivo de `deploy` a `C:\Plataformas`. El historial persistente y las claves quedan en `C:\Plataformas\FirmaDigital\data`, fuera del clon y los releases. Respaldar esa carpeta. Si se necesita conservar el historial generado en el Mac, transferir privadamente `App_Data\intentos` a `data\intentos` antes de usar la plataforma.

Verificar login, consulta de referencia y PDF en la URL final. Las pruebas automatizadas no envían invitaciones ni modifican clientes reales. La ejecución real de PowerShell/IIS debe validarse en Windows; no queda demostrada por compilar en macOS.

Referencia: [Microsoft: subaplicaciones IIS](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/advanced?view=aspnetcore-10.0#sub-applications).

## Integración con Entregas

Desplegar primero Firma Digital y después Entregas. Entregas abre `/firma-digital/` dentro de su modal. Un canal aleatorio y el origen exacto del iframe coordinan el envío de credenciales en memoria mediante `postMessage`; no se agregan a la URL ni se registran en consola. Firma comprueba el origen del padre contra `document.referrer` y exige el mismo host y protocolo (pueden diferir los puertos). Mantener la política de referrer del iframe. La autenticación sigue pasando por Legalario; después se consulta la referencia numérica y se abre Nuevo expediente. El usuario completa los datos obligatorios antes de generar.

Pruebas del intercambio sin contactar proveedores: `node --test tests/browser/entregas.test.mjs`. El tema comienza siempre en claro; cambiarlo en pantalla no persiste para la próxima apertura.

El deploy espera hasta 90 segundos a que el pool termine sus transiciones. Si falla el rollback, conserva el error original y muestra por separado el error de recuperación. No utiliza `iisreset`, pues afectaría a otras plataformas.
