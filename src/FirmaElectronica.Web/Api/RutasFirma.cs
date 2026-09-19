using System.Security.Claims;
using Microsoft.Extensions.Options;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Application.Services;
using FirmaElectronica.Domain.Agencias;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Firmantes;
using FirmaElectronica.Domain.Plantillas;
using FirmaElectronica.Domain.Usuarios;
using FirmaElectronica.Infrastructure.Legalario;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FirmaElectronica.Web.Api;
public sealed record Acceso(string Usuario, string Contrasena);
public sealed record GenerarEntrada(SolicitudDocumento Solicitud, DatosCapturados Captura, Guid? OperacionId = null);
public sealed record ConvocarEntrada(string Referencia, string Agencia, IReadOnlyCollection<Firmante> Firmantes);
public sealed record ConfirmarEntrada(string DocumentoId);
public sealed record NuevaGeneracionEntrada(string? DocumentoAnterior);

public static class RutasFirma
{
    private static UsuarioFirma Usuario(HttpContext contexto) => new(
        contexto.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException(),
        contexto.User.FindFirstValue(ClaimTypes.Name) ?? "", contexto.User.FindFirstValue(ClaimTypes.Role) ?? "",
        contexto.User.FindFirstValue("agencias") ?? "");
    private static string Token(HttpContext contexto) => contexto.Session.GetString("LegalarioToken") ?? throw new UnauthorizedAccessException("La sesión expiró. Inicie sesión nuevamente.");
    private static IReadOnlyCollection<string> Plantillas(string agencia) => PlantillasPorAgencia.Obtener(agencia).Values.Distinct().ToArray();
    private static async Task<JsonElement> AutorizarDocumento(HttpContext contexto, ILegalarioClient legalario, string agencia, string id, CancellationToken ct, bool esperar = false)
    {
        Usuario(contexto).ValidarAgencia(agencia);
        JsonElement documento;
        for (var intento = 1; ; intento++)
        {
            try { documento = await legalario.ConsultarDocumentoAsync(id, Token(contexto), ct); break; }
            catch (OperacionLegalarioException error) when (esperar && intento < 3 && error.Reintentable)
            { await Task.Delay(TimeSpan.FromSeconds(3), ct); }
        }
        var plantilla = documento.TryGetProperty("organization_document_id", out var p) ? p.ToString() :
            documento.TryGetProperty("template_id", out p) ? p.ToString() : "";
        if (!Plantillas(agencia.Trim().ToUpperInvariant()).Contains(plantilla))
            throw new UnauthorizedAccessException("El documento no corresponde a las plantillas de la agencia.");
        return documento;
    }
    public static void MapearFirma(this WebApplication app)
    {
        app.MapGet("/api/sesion/csrf", (HttpContext c, IAntiforgery antiforgery) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(c).RequestToken });
        });
        app.MapPost("/api/sesion", async (Acceso acceso, HttpContext c, IConsultaPerfilUsuario autenticador, AutorizacionLegalario autorizacion, IOptions<AccesoTemporalOptions> modo, CancellationToken ct) =>
        {
            var temporal = modo.Value;
            if (temporal.Activo && (string.IsNullOrWhiteSpace(temporal.Usuario) || string.IsNullOrWhiteSpace(temporal.Contrasena)))
                throw new InvalidOperationException("Falta configurar la cuenta temporal en el servidor.");
            var nombre = temporal.Activo ? temporal.Usuario : acceso.Usuario;
            var contrasena = temporal.Activo ? temporal.Contrasena : acceso.Contrasena;
            var token = await autorizacion.IniciarSesionAsync(nombre, contrasena, ct);
            if (token is null) return Results.Unauthorized();
            var usuario = await autenticador.ConsultarAsync(nombre, ct);
            if (usuario is null) return Results.Forbid();
            c.Session.Clear();
            c.Session.SetString("LegalarioToken", token);
            var identidad = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Usuario), new Claim(ClaimTypes.Name, usuario.Nombre),
                new Claim(ClaimTypes.Role, usuario.Rol), new Claim("agencias", usuario.Agencias)
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            if (temporal.Activo) identidad.AddClaim(new Claim("acceso_temporal", temporal.Usuario));
            await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
            return Results.Ok(new { usuario.Usuario, usuario.Nombre, usuario.Rol, Agencias = usuario.AgenciasPermitidas });
        }).RequireRateLimiting("acceso");
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapDelete("/sesion", async (HttpContext c) =>
        {
            c.Session.Clear();
            await c.SignOutAsync();
            return Results.NoContent();
        });
        api.MapGet("/sesion", (HttpContext c) => Results.Ok(Usuario(c)));
        api.MapGet("/agencias", (HttpContext c) => Usuario(c).AgenciasPermitidas.Select(a => CatalogoAgencias.Todas[a]));
        api.MapGet("/agencias/{agencia}/plantillas", (string agencia, HttpContext c) =>
        {
            Usuario(c).ValidarAgencia(agencia);
            return Results.Ok(PlantillasPorAgencia.Obtener(agencia).Select(p => new { tipo = p.Key.ToString(), plantillaId = p.Value }));
        });
        api.MapGet("/referencias/{referencia}", async (string referencia, HttpContext c, IReferenciaDataProvider proveedor, CancellationToken ct) =>
        {
            var datos = await proveedor.ObtenerDatosReferenciaAsync(referencia, ct);
            var agenciaReferencia = PreparadorVariables.Texto(datos, "dealer").Trim().ToUpperInvariant();
            if (!Usuario(c).AgenciasPermitidas.Contains(agenciaReferencia))
                return Results.Json(new { mensaje = "No tienes acceso a la agencia de esta referencia." }, statusCode: 403);
            return Results.Ok(datos);
        });
        api.MapPost("/firmantes/preparar", async (GenerarEntrada entrada, HttpContext c, GeneracionDocumentos generacion, IPlantillaResolver resolver, PreparadorFirmantes firmantes, CancellationToken ct) =>
        {
            var preparado = await generacion.PrepararAsync(Usuario(c), entrada.Solicitud, entrada.Captura, ct);
            return Results.Ok(firmantes.Preparar(resolver.Resolver(entrada.Solicitud, preparado.Datos), preparado.Datos));
        });
        api.MapPost("/documentos/preparar", async (GenerarEntrada entrada, HttpContext c, GeneracionDocumentos generacion, CancellationToken ct) =>
            Results.Ok(await generacion.PrepararAsync(Usuario(c), entrada.Solicitud, entrada.Captura, ct)));
        api.MapPost("/documentos/trabajos", async (GenerarEntrada entrada, HttpContext c, GeneracionDocumentos generacion, ColaGeneracionDocumentos cola, CancellationToken ct) =>
        {
            var preparado = await generacion.PrepararAsync(Usuario(c), entrada.Solicitud, entrada.Captura, ct);
            var trabajo = cola.Encolar(Usuario(c).Usuario, preparado.Documento with { OperacionId = entrada.OperacionId ?? Guid.NewGuid() }, Token(c));
            return Results.Accepted($"/api/documentos/trabajos/{trabajo.Id}", trabajo);
        });
        api.MapGet("/documentos/trabajos/{id:guid}", (Guid id, HttpContext c, ColaGeneracionDocumentos cola) =>
            Results.Ok(cola.Consultar(id, Usuario(c).Usuario)));
        api.MapPost("/documentos", async (GenerarEntrada entrada, HttpContext c, GeneracionDocumentos generacion, CancellationToken ct) =>
        {
            var preparado = await generacion.PrepararAsync(Usuario(c), entrada.Solicitud, entrada.Captura, ct);
            return Results.Ok(await generacion.CrearAsync(Usuario(c).Usuario, preparado.Documento with { OperacionId = entrada.OperacionId ?? Guid.NewGuid() }, Token(c), ct));
        });
        api.MapGet("/documentos", async (string agencia, int? pagina, int? tamano, string? busqueda, string? plantilla, HttpContext c, ConsultaDocumentos consulta, CancellationToken ct) =>
        {
            Usuario(c).ValidarAgencia(agencia);
            var permitidas = Plantillas(agencia.Trim().ToUpperInvariant());
            if (plantilla is not null && !permitidas.Contains(plantilla)) throw new UnauthorizedAccessException();
            return Results.Ok(await consulta.ConsultarAsync(plantilla is null ? permitidas : new[] { plantilla }, pagina ?? 1, tamano ?? 15, busqueda, Token(c), ct));
        });
        api.MapGet("/documentos/{id}/vista", async (string id, string agencia, HttpContext c, ILegalarioClient legalario, CancellationToken ct) =>
        {
            await AutorizarDocumento(c, legalario, agencia, id, ct);
            c.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { url = await legalario.ObtenerUrlDocumentoAsync(id, Token(c), ct) });
        });
        api.MapGet("/documentos/{id}/pdf", async (string id, string agencia, HttpContext c, ILegalarioClient legalario, CancellationToken ct) =>
        {
            await AutorizarDocumento(c, legalario, agencia, id, ct);
            var pdf = await legalario.DescargarPdfAsync(id, Token(c), ct);
            c.Response.Headers.CacheControl = "no-store";
            return pdf is null ? Results.Accepted(value: new { preparando = true }) : Results.File(pdf, "application/pdf");
        });
        api.MapGet("/documentos/{id}/firmas", async (string id, string agencia, HttpContext c, ILegalarioClient legalario, CancellationToken ct) =>
        {
            await AutorizarDocumento(c, legalario, agencia, id, ct);
            return Results.Ok(await legalario.ConsultarFirmasAsync(id, Token(c), ct));
        });
        api.MapGet("/documentos/{id}/preparar-firmantes", async (string id, string agencia, HttpContext c, ILegalarioClient legalario, IRegistroIntentos registro, IReferenciaDataProvider referencias, IPlantillaResolver resolver, PreparadorFirmantes preparador, CancellationToken ct) =>
        {
            var documento = await AutorizarDocumento(c, legalario, agencia, id, ct);
            var referencia = await registro.ReferenciaDocumentoAsync(id, ct);
            if (string.IsNullOrWhiteSpace(referencia) && documento.TryGetProperty("referencia", out var refDocumento)) referencia = refDocumento.ToString();
            if (string.IsNullOrWhiteSpace(referencia))
                return Results.Conflict(new { mensaje = "Este documento no tiene un expediente asociado en la migración. Necesitamos recuperar esa asociación antes de preparar sus firmantes; no generes otro documento." });
            var datos = await referencias.ObtenerDatosReferenciaAsync(referencia, ct);
            if (!PreparadorVariables.Texto(datos, "dealer").Trim().Equals(agencia.Trim(), StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
            var nombre = $"Documentación_{PreparadorVariables.Texto(datos, "nombre_completo")}_{PreparadorVariables.Texto(datos, "vin")}";
            if (!documento.TryGetProperty("name", out var n) || !IdentidadDocumento.Coincide(n.GetString(), nombre)) throw new ArgumentException("Los datos del expediente no coinciden con el documento. Requiere revisión.");
            var regla = resolver.Resolver(new(referencia, agencia, "", DateOnly.FromDateTime(DateTime.Today), false), datos);
            var plantilla = documento.TryGetProperty("organization_document_id", out var pd) ? pd.ToString() : documento.GetProperty("template_id").ToString();
            if (regla.LegalarioTemplateId != plantilla) throw new ArgumentException("La plantilla no coincide con el expediente.");
            return Results.Ok(new { referencia, firmantes = preparador.Preparar(regla, datos) });
        });
        api.MapPost("/documentos/{id}/convocar", async (string id, ConvocarEntrada entrada, HttpContext c, ILegalarioClient legalario, IReferenciaDataProvider referencias, IPlantillaResolver resolver, PreparadorFirmantes preparadorFirmantes, ServicioConvocatoria convocatoria, CancellationToken ct) =>
        {
            var documento = await AutorizarDocumento(c, legalario, entrada.Agencia, id, ct, esperar: true);
            var datos = await referencias.ObtenerDatosReferenciaAsync(entrada.Referencia, ct);
            if (!PreparadorVariables.Texto(datos, "dealer").Trim().Equals(entrada.Agencia.Trim(), StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
            // Evita actualizar en Quiter el contacto de una referencia distinta al documento mostrado.
            var nombre = $"Documentación_{PreparadorVariables.Texto(datos, "nombre_completo")}_{PreparadorVariables.Texto(datos, "vin")}";
            if (!documento.TryGetProperty("name", out var n) || !IdentidadDocumento.Coincide(n.GetString(), nombre)) throw new ArgumentException("El documento no coincide con la referencia.");
            var regla = resolver.Resolver(new(entrada.Referencia, entrada.Agencia, "", DateOnly.FromDateTime(DateTime.Today), false), datos);
            var plantillaDocumento = documento.TryGetProperty("organization_document_id", out var pd) ? pd.ToString() : documento.GetProperty("template_id").ToString();
            if (regla.LegalarioTemplateId != plantillaDocumento) throw new ArgumentException("La plantilla del documento ya no coincide con los datos de referencia. Revise el expediente.");
            var tiposEsperados = preparadorFirmantes.Preparar(regla, datos).Select(f => f.TipoFirmante).Order().ToArray();
            if (!tiposEsperados.SequenceEqual(entrada.Firmantes.Select(f => f.TipoFirmante).Order()))
                throw new ArgumentException("Los tipos de firmante no corresponden a la plantilla.");
            var cuenta = PreparadorVariables.Texto(datos, "cta_cliente");
            if (string.IsNullOrWhiteSpace(cuenta)) cuenta = PreparadorVariables.Texto(datos, "CTA_CLIENTE");
            return Results.Ok(await convocatoria.ConvocarAsync(id, cuenta, entrada.Firmantes, Token(c), ct));
        });
        api.MapPost("/documentos/{id}/firmantes/{firmanteId}/reenviar", async (string id, string firmanteId, string agencia, HttpContext c, ILegalarioClient legalario, CancellationToken ct) =>
        {
            await AutorizarDocumento(c, legalario, agencia, id, ct);
            var estado = await legalario.ConsultarFirmasAsync(id, Token(c), ct);
            var firmante = estado.Firmantes.SingleOrDefault(f => f.TryGetProperty("id", out var valor) && valor.GetString() == firmanteId);
            if (firmante.ValueKind == JsonValueKind.Undefined) throw new UnauthorizedAccessException();
            if (firmante.TryGetProperty("status", out var estatus) && estatus.GetString() == "confirmed") throw new ArgumentException("El firmante ya confirmó su firma.");
            await legalario.ReenviarInvitacionAsync(firmanteId, Token(c), ct);
            return Results.NoContent();
        });
        api.MapDelete("/documentos/{id}", async (string id, string agencia, HttpContext c, ILegalarioClient legalario, CancellationToken ct) =>
        {
            await AutorizarDocumento(c, legalario, agencia, id, ct);
            await legalario.EliminarDocumentoAsync(id, Token(c), ct);
            return Results.NoContent();
        });
        api.MapGet("/intentos/{referencia}", async (string referencia, string plantilla, string agencia, HttpContext c, IRegistroIntentos registro, CancellationToken ct) =>
        {
            Usuario(c).ValidarAgencia(agencia);
            if (!Plantillas(agencia.Trim().ToUpperInvariant()).Contains(plantilla)) throw new UnauthorizedAccessException();
            var intento = await registro.LeerAsync(GeneracionDocumentos.Clave(Usuario(c).Usuario, referencia, plantilla), ct);
            return intento is null ? Results.NotFound() : Results.Ok(new { intento.Estado, intento.IniciadoEn, intento.Documento });
        });
        api.MapPost("/intentos/{referencia}/nueva-generacion", async (string referencia, string plantilla, string agencia, NuevaGeneracionEntrada entrada, HttpContext c, GeneracionDocumentos generacion, CancellationToken ct) =>
        {
            Usuario(c).ValidarAgencia(agencia);
            if (!Plantillas(agencia.Trim().ToUpperInvariant()).Contains(plantilla)) throw new UnauthorizedAccessException();
            return Results.Ok(await generacion.AutorizarNuevaGeneracionAsync(Usuario(c).Usuario, referencia, plantilla, entrada.DocumentoAnterior, ct));
        });
        api.MapGet("/intentos/{referencia}/candidatos", async (string referencia, string plantilla, string agencia, HttpContext c, ConciliacionDocumentos conciliacion, CancellationToken ct) =>
        {
            Usuario(c).ValidarAgencia(agencia);
            if (!Plantillas(agencia.Trim().ToUpperInvariant()).Contains(plantilla)) throw new UnauthorizedAccessException();
            return Results.Ok(await conciliacion.CandidatosAsync(GeneracionDocumentos.Clave(Usuario(c).Usuario, referencia, plantilla), Token(c), ct));
        });
        api.MapPost("/intentos/{referencia}/confirmar", async (string referencia, string plantilla, string agencia, ConfirmarEntrada entrada, HttpContext c, ConciliacionDocumentos conciliacion, CancellationToken ct) =>
        {
            Usuario(c).ValidarAgencia(agencia);
            if (!Plantillas(agencia.Trim().ToUpperInvariant()).Contains(plantilla)) throw new UnauthorizedAccessException();
            return Results.Ok(await conciliacion.ConfirmarAsync(GeneracionDocumentos.Clave(Usuario(c).Usuario, referencia, plantilla), entrada.DocumentoId, Token(c), ct));
        });
    }
}
