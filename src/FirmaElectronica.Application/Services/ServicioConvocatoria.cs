using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Firmantes;
namespace FirmaElectronica.Application.Services;

public sealed record ResultadoConvocatoria(bool ContactoActualizado, string? AvisoContacto);
public sealed class ServicioConvocatoria(ILegalarioClient legalario, IQuiterClient quiter, IRegistroIntentos registro)
{
    public Task<ResultadoConvocatoria> ConvocarAsync(string documentoId, string? cuentaCliente, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentoId);
        var clave = GeneracionDocumentos.Clave("CONVOCATORIA", documentoId, "FIRMANTES");
        return registro.ExclusivoAsync(clave, async cancelacion =>
        {
            var anterior = await registro.LeerAsync(clave, cancelacion);
            if (anterior is not null) throw new InvalidOperationException("Existe una convocatoria registrada. Consulte los firmantes antes de reenviar.");
            return await EjecutarAsync(clave, documentoId, cuentaCliente, firmantes, token, cancelacion);
        }, ct);
    }
    private async Task<ResultadoConvocatoria> EjecutarAsync(string clave, string documentoId, string? cuentaCliente, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct)
    {
        ValidadorFirmantes.Validar(firmantes);
        if (firmantes.Count(f => f.TipoFirmante == TipoFirmante.Cliente) != 1)
            throw new ArgumentException("Debe existir un único firmante cliente.");
        // Una segunda convocatoria no debe duplicar firmantes; el reenvío tiene una operación propia.
        var existentes = await legalario.ConsultarFirmasAsync(documentoId, token, ct);
        if (existentes.Convocados > 0) throw new InvalidOperationException("El documento ya tiene firmantes. Use el reenvío de invitaciones.");
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            while (await legalario.ObtenerUrlDocumentoAsync(documentoId, token, limite.Token) is null &&
                   await legalario.DescargarPdfAsync(documentoId, token, limite.Token) is null)
                await Task.Delay(TimeSpan.FromSeconds(3), limite.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new InvalidOperationException("Legalario no entregó una liga ni un PDF disponible. Consulta el documento e intenta nuevamente."); }
        var actualizado = false;
        string? aviso = string.IsNullOrWhiteSpace(cuentaCliente)
            ? "No se actualizó el contacto en Quiter porque el expediente no contiene la cuenta del cliente."
            : null;
        if (!string.IsNullOrWhiteSpace(cuentaCliente))
        {
            var cliente = firmantes.Single(f => f.TipoFirmante == TipoFirmante.Cliente);
            try
            {
                await quiter.ActualizarContactoClienteAsync(new(cuentaCliente, cliente.Correo, cliente.Telefono), ct);
                actualizado = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            { aviso = "La convocatoria continúa, pero no se confirmó la actualización del contacto en Quiter."; }
        }
        var intento = new IntentoDocumento(clave, "", documentoId, "Convocatoria", "FIRMANTES", DateTimeOffset.UtcNow, "EnCurso", null);
        await registro.GuardarAsync(intento, ct);
        await legalario.ConvocarFirmantesAsync(documentoId, firmantes, token, ct);
        await registro.GuardarAsync(intento with { Estado = aviso is null ? "Convocado" : "ConvocadoSinContactoQuiter" }, CancellationToken.None);
        return new(actualizado, aviso);
    }
}
