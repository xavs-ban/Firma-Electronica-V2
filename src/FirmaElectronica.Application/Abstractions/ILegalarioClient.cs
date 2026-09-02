using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Firmantes;

namespace FirmaElectronica.Application.Abstractions;

public interface ILegalarioClient
{
    Task<DocumentoGenerado> CrearDocumentoAsync(SolicitudDocumento solicitud, IReadOnlyDictionary<int, string> variables, CancellationToken cancellationToken);

    Task ConvocarFirmantesAsync(string legalarioDocumentId, IReadOnlyCollection<Firmante> firmantes, CancellationToken cancellationToken);
}
