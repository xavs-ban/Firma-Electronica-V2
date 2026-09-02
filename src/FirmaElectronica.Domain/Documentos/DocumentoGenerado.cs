namespace FirmaElectronica.Domain.Documentos;

public sealed record DocumentoGenerado(
    string Referencia,
    string Nombre,
    string LegalarioDocumentId,
    DateTimeOffset CreadoEn);
