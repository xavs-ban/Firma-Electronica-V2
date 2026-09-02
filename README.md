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
