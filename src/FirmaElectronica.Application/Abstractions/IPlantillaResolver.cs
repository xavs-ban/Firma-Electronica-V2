using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Plantillas;

namespace FirmaElectronica.Application.Abstractions;

public interface IPlantillaResolver
{
    ReglaPlantilla Resolver(SolicitudDocumento solicitud, IReadOnlyDictionary<string, object?> datosReferencia);
}
