# Firma Electronica V2

Migracion de la plataforma de Firma Electronica a .NET, manteniendo todas las funcionalidades actuales y preparando una interfaz mas amigable tipo iOS.

## Objetivo

Redisenar y migrar la plataforma actual hacia una arquitectura mas robusta, estable y mantenible, conservando el comportamiento operativo existente para Nissan, Hyundai y las agencias ya configuradas.

## Stack inicial

- .NET 10
- ASP.NET Core Razor Pages
- Arquitectura por capas: Web, Application, Domain e Infrastructure
- Pruebas automatizadas con xUnit

## Principios de migracion

- No quitar funcionalidades existentes.
- No cambiar reglas de negocio sin validacion.
- Separar reglas de plantillas, agencias, SPs y Legalario para que sean faciles de probar.
- Mantener ambientes `dev` y `prod` listos para AWS.
- Proteger integraciones externas con logs, timeouts y mensajes claros para usuario.

## Estado y ejecución

El backend dispone de rutas autenticadas y trabajos de generación; la interfaz incluye acceso, preparación de referencias, documentos y actividad. El login utiliza la API de Legalario y requiere configuración privada. Consultar [estado de migración](docs/CONTINUIDAD.md), [API y configuración](docs/API_BACKEND.md) y [seguro en SQL](docs/SEGURO_SQL.md).

```sh
dotnet test
dotnet run --project src/FirmaElectronica.Web --launch-profile http
```

La aplicación local escucha en `http://localhost:5022`. Completar la configuración privada antes de probar integraciones reales.

## Clonar y configurar

```sh
git clone https://github.com/xavs-ban/Firma-Electronica-V2.git
cd Firma-Electronica-V2
cp src/FirmaElectronica.Web/appsettings.example.json src/FirmaElectronica.Web/appsettings.Local.json
```

Completar la copia local con los valores privados; está excluida de Git y de la publicación. Instalar el SDK de `global.json` y ejecutar los comandos de pruebas/arranque anteriores.

## Despliegue

Ver [preparación de AWS EC2](docs/AWS.md). `deploy/` incluye servicio systemd, configuración Nginx, ejemplo de variables de entorno y script de publicación con pruebas. No incluye secretos ni crea infraestructura automáticamente.

El logo de Grupo Huerpel fue proporcionado por el propietario del proyecto desde [Wix](https://static.wixstatic.com/media/f44ea7_da67916fa6f445dbb59b839318acbb5d~mv2.jpg) y se sirve localmente.

Para la instancia Windows con IIS y puerto 3366, seguir [AWS-IIS](./docs/AWS-IIS.md). La guía Linux es una alternativa y no corresponde al servidor actual.
