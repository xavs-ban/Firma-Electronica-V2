using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Application.Services;
using FirmaElectronica.Infrastructure.Datos;
using FirmaElectronica.Infrastructure.Intentos;
using FirmaElectronica.Infrastructure.Legalario;
using FirmaElectronica.Infrastructure.Quiter;
using FirmaElectronica.Web.Api;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddRazorPages();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
// Nginx local es el único proxy confiable por defecto.
var claves = builder.Configuration["DataProtection:Carpeta"];
if (!string.IsNullOrWhiteSpace(claves))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(claves)).SetApplicationName("FirmaElectronicaV2");
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opciones =>
{
    opciones.IdleTimeout = TimeSpan.FromMinutes(30);
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SameSite = SameSiteMode.Strict;
    opciones.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opciones =>
{
    opciones.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SameSite = SameSiteMode.Strict;
    opciones.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    opciones.Events.OnRedirectToLogin = contexto => { contexto.Response.StatusCode = 401; return Task.CompletedTask; };
    opciones.Events.OnRedirectToAccessDenied = contexto => { contexto.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(opciones => opciones.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = 429;
    opciones.AddPolicy("acceso", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton(builder.Configuration.GetSection("Legalario").Get<LegalarioOptions>() ?? new LegalarioOptions { BaseUrl = "https://api.legalario.com" });
builder.Services.AddSingleton(builder.Configuration.GetSection("Quiter").Get<QuiterOptions>() ?? new QuiterOptions());
builder.Services.AddSingleton(new DatosOptions
{
    ConnectionString = builder.Configuration.GetConnectionString("Firma") ?? "",
    TimeoutSeconds = builder.Configuration.GetValue<int?>("Datos:TimeoutSeconds") ?? 30
});
// No agregar políticas automáticas de reintento a los clientes que modifican datos.
builder.Services.AddHttpClient<ILegalarioClient, ClienteLegalario>(cliente =>
{
    cliente.Timeout = Timeout.InfiniteTimeSpan;
    cliente.MaxResponseContentBufferSize = 50 * 1024 * 1024;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ICreadorDocumentoLegalario>(servicios => servicios.GetRequiredService<ILegalarioClient>());
builder.Services.AddHttpClient<AutorizacionLegalario>(cliente => cliente.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<IQuiterClient, ClienteQuiter>(cliente => cliente.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<IConsultaReferencia, ConsultaReferenciaSql>();
builder.Services.AddScoped<IReferenciaDataProvider, ProveedorReferencias>();
builder.Services.AddScoped<IConsultaPerfilUsuario, ConsultaPerfilUsuarioSql>();
builder.Services.AddScoped<IPlantillaResolver, PlantillaResolver>();
builder.Services.AddSingleton<IRegistroIntentos>(new RegistroIntentosArchivo(
    builder.Configuration["Intentos:Carpeta"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "intentos")));
builder.Services.AddScoped<PreparadorVariables>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Firmantes").Get<ReglasFirmantes>() ?? new ReglasFirmantes());
builder.Services.AddScoped<PreparadorFirmantes>();
builder.Services.AddScoped<GeneracionDocumentos>();
builder.Services.AddScoped<ConsultaDocumentos>();
builder.Services.AddScoped<ConciliacionDocumentos>();
builder.Services.AddScoped<ServicioConvocatoria>();
builder.Services.AddSingleton<ColaGeneracionDocumentos>();
builder.Services.AddHostedService(servicios => servicios.GetRequiredService<ColaGeneracionDocumentos>());

var app = builder.Build();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (contexto, siguiente) =>
{
    try { await siguiente(contexto); }
    catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested) { }
    catch (Exception error) when (contexto.Request.Path.StartsWithSegments("/api") && !contexto.Response.HasStarted)
    {
        var estado = error switch
        {
            UnauthorizedAccessException => 403, AntiforgeryValidationException => 400,
            ArgumentException => 400, KeyNotFoundException => 404, InvalidOperationException => 409,
            CreacionDocumentoException or OperacionLegalarioException => 502, _ => 500
        };
        var mensaje = error is ArgumentException or KeyNotFoundException or InvalidOperationException or CreacionDocumentoException or OperacionLegalarioException
            ? error.Message : "No se pudo completar la operación.";
        // Evita registrar cuerpos, contraseñas, tokens o detalles del proveedor SQL.
        app.Logger.LogWarning("Operación API fallida. Tipo {Tipo}, estado {Estado}", error.GetType().Name, estado);
        contexto.Response.StatusCode = estado;
        await contexto.Response.WriteAsJsonAsync(new { mensaje, resultadoIncierto = error is CreacionDocumentoException { ResultadoIncierto: true } or OperacionLegalarioException { ResultadoIncierto: true } });
    }
});
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (contexto, siguiente) =>
{
    if (contexto.Request.Path.StartsWithSegments("/api"))
    {
        contexto.Response.Headers.CacheControl = "no-store";
        if (!contexto.Request.Path.StartsWithSegments("/api/sesion") && contexto.User.Identity?.IsAuthenticated == true && contexto.Session.GetString("LegalarioToken") is null)
        { contexto.Response.StatusCode = 401; return; }
        if (!HttpMethods.IsGet(contexto.Request.Method) && !HttpMethods.IsHead(contexto.Request.Method) && !HttpMethods.IsOptions(contexto.Request.Method))
            await contexto.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(contexto);
    }
    await siguiente(contexto);
});
app.MapearFirma();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
public partial class Program { }
