using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Firmantes;
namespace FirmaElectronica.Application.Services;

public sealed record ResultadoConvocatoria(bool ContactoActualizado, string? AvisoContacto, bool Recuperada = false, int Reenviadas = 0, int Nuevos = 0);
public sealed class ServicioConvocatoria(ILegalarioClient legalario, IQuiterClient quiter, IRegistroIntentos registro)
{
    public async Task<ResultadoConvocatoria> ConvocarAsync(string documentoId, string? cuentaCliente, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentoId);
        var clave = GeneracionDocumentos.Clave("CONVOCATORIA", documentoId, "FIRMANTES");
        using var plazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        plazo.CancelAfter(TimeSpan.FromSeconds(180));
        try
        {
            return await registro.ExclusivoAsync(clave, async cancelacion =>
            {
                ValidadorFirmantes.Validar(firmantes);
                if (firmantes.Count(f => f.TipoFirmante == TipoFirmante.Cliente) != 1)
                    throw new ArgumentException("Debe existir un único firmante cliente.");
                return await EjecutarAsync(clave, documentoId, cuentaCliente, firmantes, token, cancelacion);
            }, plazo.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new OperacionLegalarioException("La operación excedió el tiempo de espera. Puedes volver a intentar; se consultarán los firmantes para continuar con los pendientes.", resultadoIncierto: true);
        }
    }
    private async Task<ResultadoConvocatoria> EjecutarAsync(string clave, string documentoId, string? cuentaCliente, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct)
    {
        var actualizado = false;
        string? aviso = string.IsNullOrWhiteSpace(cuentaCliente)
            ? "No se actualizó el contacto en Quiter porque el expediente no contiene la cuenta del cliente."
            : null;
        if (!string.IsNullOrWhiteSpace(cuentaCliente))
        {
            var cliente = firmantes.Single(f => f.TipoFirmante == TipoFirmante.Cliente);
            try
            {
                using var plazoQuiter = CancellationTokenSource.CreateLinkedTokenSource(ct);
                plazoQuiter.CancelAfter(TimeSpan.FromSeconds(15));
                await quiter.ActualizarContactoClienteAsync(new(cuentaCliente, cliente.Correo, cliente.Telefono), plazoQuiter.Token);
                actualizado = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            { aviso = "La convocatoria continúa, pero no se confirmó la actualización del contacto en Quiter."; }
        }
        var intento = new IntentoDocumento(clave, "", documentoId, "Convocatoria", "FIRMANTES", DateTimeOffset.UtcNow, "EnCurso", null);
        try
        {
            // Consultar el proveedor en cada intento: el historial local no decide si se puede enviar.
            var reenviados = new HashSet<string>();
            var nuevos = 0;
            for (var numero = 1; ; numero++)
            {
                try
                {
                    var existentes = await EsperarFirmantesAsync(documentoId, token, ct);
                    await registro.GuardarAsync(intento, ct);
                    foreach (var firmante in existentes.Firmantes)
                    {
                        if (firmante.TryGetProperty("status", out var estado) && estado.GetString() == "confirmed") continue;
                        var id = firmante.GetProperty("id").GetString();
                        if (string.IsNullOrWhiteSpace(id)) throw new OperacionLegalarioException("Legalario no entregó el identificador del firmante. Vuelve a intentar.");
                        if (reenviados.Contains(id)) continue;
                        await legalario.ReenviarInvitacionAsync(id, token, ct);
                        reenviados.Add(id);
                    }
                    var faltantes = firmantes.Where(f => !existentes.Firmantes.Any(e =>
                        e.TryGetProperty("type", out var tipo) && tipo.GetString() == ValidadorFirmantes.Tipo(f.TipoFirmante))).ToArray();
                    if (faltantes.Length > 0)
                    {
                        await legalario.ConvocarFirmantesAsync(documentoId, faltantes, token, ct);
                        nuevos = faltantes.Length;
                    }
                    await registro.GuardarAsync(intento with { Estado = aviso is null ? "Convocado" : "ConvocadoSinContactoQuiter" }, CancellationToken.None);
                    return new(actualizado, aviso, reenviados.Count > 0, reenviados.Count, nuevos);
                }
                catch (OperacionLegalarioException error) when (numero < 3 && (error.Reintentable || error.ResultadoIncierto))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
            }
        }
        catch (OperacionLegalarioException error)
        {
            await registro.GuardarAsync(intento with { Estado = error.ResultadoIncierto ? "Incierto" : error.Reintentable ? "RechazadoTemporal" : "Rechazado" }, CancellationToken.None);
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            await registro.GuardarAsync(intento with { Estado = "Incierto" }, CancellationToken.None);
            throw new OperacionLegalarioException("No se pudo confirmar la convocatoria. Puedes volver a intentar el envío.", resultadoIncierto: true);
        }
    }
    private async Task<EstadoFirmas> EsperarFirmantesAsync(string documentoId, string token, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            while (true)
            {
                try { return await legalario.ConsultarFirmasAsync(documentoId, token, limite.Token); }
                catch (OperacionLegalarioException error) when (error.Reintentable)
                { await Task.Delay(TimeSpan.FromSeconds(3), limite.Token); }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new OperacionLegalarioException("Legalario todavía no permite consultar los firmantes. Intenta nuevamente en unos momentos.", reintentable: true);
        }
    }
}
