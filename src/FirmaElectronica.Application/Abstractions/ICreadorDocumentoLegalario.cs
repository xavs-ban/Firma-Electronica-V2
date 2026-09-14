using FirmaElectronica.Domain.Documentos;

namespace FirmaElectronica.Application.Abstractions;

public interface ICreadorDocumentoLegalario
{
    Task<DocumentoGenerado> CrearDocumentoAsync(
        DocumentoParaCrear documento, string token, CancellationToken cancellationToken);
}
