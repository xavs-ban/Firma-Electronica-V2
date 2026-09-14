using FirmaElectronica.Domain.Usuarios;
namespace FirmaElectronica.Application.Abstractions;
public interface IConsultaPerfilUsuario
{
    Task<UsuarioFirma?> ConsultarAsync(string usuario, CancellationToken cancellationToken);
}
