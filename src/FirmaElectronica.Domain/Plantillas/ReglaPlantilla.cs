namespace FirmaElectronica.Domain.Plantillas;

public sealed record ReglaPlantilla(
    string Agencia,
    TipoPlantilla Tipo,
    string LegalarioTemplateId,
    bool IncluyeRepresentanteLegal);
