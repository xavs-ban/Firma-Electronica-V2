using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Firmantes;
namespace FirmaElectronica.Application.Services;

public sealed record ResultadoConvocatoria(bool ContactoActualizado, string? AvisoContacto, bool Recuperada = false);
public sealed class ServicioConvocatoria(ILegalarioClient legalario, IQuiterClient quiter, IRegistroIntentos registro)
{
    public async Task<ResultadoConvocatoria> ConvocarAsync(string documentoId, string? cuentaCliente, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentoId);
        var clave = GeneracionDocumentos.Clave("CONVOCATORIA", documentoId, "FIRMANTES");
        using var plazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        plazo.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            return await registro.ExclusivoAsync(clave, async cancelacion =>
            {
                ValidadorFirmantes.Validar(firmantes);
                if (firmantes.Count(f => f.TipoFirmante == TipoFirmante.Cliente) != 1)
                    throw new ArgumentException("Debe existir un único firmante cliente.");
                var anterior = await registro.LeerAsync(clave, cancelacion);
                if (anterior?.Estado is "Convocado" or "ConvocadoSinContactoQuiter" or "Conciliado")
                    throw new InvalidOperationException("Existe una convocatoria registrada. Consulte los firmantes antes de reenviar.");
                var existentes = await EsperarFirmantesAsync(documentoId, token, cancelacion);
                if (existentes.Convocados > 0)
                {
                    var recuperado = anterior ?? new IntentoDocumento(clave, "", documentoId, "Convocatoria", "FIRMANTES", DateTimeOffset.UtcNow, "Conciliado", null);
                    await registro.GuardarAsync(recuperado with { Estado = "Conciliado" }, CancellationToken.None);
                    return new ResultadoConvocatoria(false, "Se encontraron firmantes registrados. No se enviaron nuevas invitaciones ni se actualizó el contacto en Quiter.", true);
                }
                // Una lectura vacía no demuestra que un POST incierto no se esté procesando.
                if (anterior is not null && anterior.Estado is not ("Rechazado" or "RechazadoTemporal"))
                    throw new OperacionLegalarioException("La convocatoria anterior sigue sin confirmarse. Consulta las firmas más tarde antes de repetir el envío.", resultadoIncierto: true);
                return await EjecutarAsync(clave, documentoId, cuentaCliente, firmantes, token, cancelacion);
            }, plazo.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new OperacionLegalarioException("La operación excedió el tiempo de espera. Consulta las firmas para comprobar si Legalario recibió la convocatoria antes de intentar otro envío.", resultadoIncierto: true);
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
        await registro.GuardarAsync(intento, ct);
        try
        {
            await legalario.ConvocarFirmantesAsync(documentoId, firmantes, token, ct);
        }
        catch (OperacionLegalarioException error)
        {
            await registro.GuardarAsync(intento with { Estado = error.ResultadoIncierto ? "Incierto" : error.Reintentable ? "RechazadoTemporal" : "Rechazado" }, CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            await registro.GuardarAsync(intento with { Estado = "Incierto" }, CancellationToken.None);
            throw new OperacionLegalarioException("No se pudo confirmar la convocatoria. Consulta las firmas antes de repetir el envío.", resultadoIncierto: true);
        }
        await registro.GuardarAsync(intento with { Estado = aviso is null ? "Convocado" : "ConvocadoSinContactoQuiter" }, CancellationToken.None);
        return new(actualizado, aviso);
    }
    private async Task<EstadoFirmas> EsperarFirmantesAsync(string documentoId, string token, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(45));
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
            throw new OperacionLegalarioException("Legalario sigue preparando el documento. No se enviaron invitaciones; intenta nuevamente en unos momentos.", reintentable: true);
        }
    }
}
