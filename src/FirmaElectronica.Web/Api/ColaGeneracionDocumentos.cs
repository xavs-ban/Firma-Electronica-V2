using System.Collections.Concurrent;
using System.Threading.Channels;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Application.Services;
using FirmaElectronica.Domain.Documentos;
namespace FirmaElectronica.Web.Api;

public sealed record EstadoTrabajo(Guid Id, string Estado, DocumentoGenerado? Documento = null, string? Mensaje = null, bool ResultadoIncierto = false);
public sealed class ColaGeneracionDocumentos(IServiceScopeFactory ambitos, ILogger<ColaGeneracionDocumentos> logger) : BackgroundService
{
    private sealed record Trabajo(Guid Id, string Usuario, DocumentoParaCrear Documento, string Token);
    private sealed record Registro(string Usuario, EstadoTrabajo Estado, DateTimeOffset ActualizadoEn);
    private readonly Channel<Trabajo> cola = Channel.CreateBounded<Trabajo>(new BoundedChannelOptions(100)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly ConcurrentDictionary<Guid, Registro> trabajos = new();
    public EstadoTrabajo Encolar(string usuario, DocumentoParaCrear documento, string token)
    {
        foreach (var (id, registro) in trabajos)
            if (registro.Estado.Estado is not ("EnCola" or "Procesando") && registro.ActualizadoEn < DateTimeOffset.UtcNow.AddHours(-1)) trabajos.TryRemove(id, out _);
        var trabajo = new Trabajo(Guid.NewGuid(), usuario, documento, token);
        var estado = new EstadoTrabajo(trabajo.Id, "EnCola");
        trabajos[trabajo.Id] = new(usuario, estado, DateTimeOffset.UtcNow);
        if (!cola.Writer.TryWrite(trabajo))
        {
            trabajos.TryRemove(trabajo.Id, out _);
            throw new InvalidOperationException("La cola de generación está llena. Espere a que termine un trabajo.");
        }
        return estado;
    }
    public EstadoTrabajo Consultar(Guid id, string usuario)
    {
        if (!trabajos.TryGetValue(id, out var registro) || registro.Usuario != usuario)
            throw new KeyNotFoundException("El trabajo no está disponible. Si el servidor se reinició, consulte el intento de la referencia.");
        return registro.Estado;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var trabajo in cola.Reader.ReadAllAsync(stoppingToken))
        {
            Actualizar(trabajo, new(trabajo.Id, "Procesando"));
            try
            {
                using var ambito = ambitos.CreateScope();
                var generacion = ambito.ServiceProvider.GetRequiredService<GeneracionDocumentos>();
                var documento = await generacion.CrearAsync(trabajo.Usuario, trabajo.Documento, trabajo.Token, stoppingToken);
                Actualizar(trabajo, new(trabajo.Id, "Completado", documento));
            }
            catch (CreacionDocumentoException error)
            { Actualizar(trabajo, new(trabajo.Id, "Error", Mensaje: error.Message, ResultadoIncierto: error.ResultadoIncierto)); }
            catch (InvalidOperationException error)
            { Actualizar(trabajo, new(trabajo.Id, "RequiereRevision", Mensaje: error.Message)); }
            catch (Exception error)
            {
                logger.LogWarning("Trabajo de generación interrumpido. Tipo {Tipo}", error.GetType().Name);
                Actualizar(trabajo, new(trabajo.Id, "RequiereRevision", Mensaje: "Consulte el intento antes de generar nuevamente.", ResultadoIncierto: true));
            }
        }
    }
    private void Actualizar(Trabajo trabajo, EstadoTrabajo estado) => trabajos[trabajo.Id] = new(trabajo.Usuario, estado, DateTimeOffset.UtcNow);
}
