namespace FirmaElectronica.Domain.Documentos;

public sealed record SolicitudDocumento(
    string Referencia,
    string Agencia,
    string TipoVentaSeleccionado,
    DateOnly FechaOperacion,
    bool AplicaSeguro);
