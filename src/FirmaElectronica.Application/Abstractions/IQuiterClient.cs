namespace FirmaElectronica.Application.Abstractions;

public interface IQuiterClient
{
    Task ActualizarContactoClienteAsync(ContactoClienteQuiter contacto, CancellationToken cancellationToken);
}

public sealed record ContactoClienteQuiter(
    string CuentaCliente,
    string? Correo,
    string? Telefono);

// Mensaje controlado, sin cuerpos remotos, contactos ni secretos.
public sealed class ActualizacionQuiterException(string mensaje) : InvalidOperationException(mensaje);
