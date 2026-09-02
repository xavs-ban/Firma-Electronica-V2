namespace FirmaElectronica.Application.Abstractions;

public interface IQuiterClient
{
    Task ActualizarContactoClienteAsync(ContactoClienteQuiter contacto, CancellationToken cancellationToken);
}

public sealed record ContactoClienteQuiter(
    string CuentaCliente,
    string? Correo,
    string? Telefono);
