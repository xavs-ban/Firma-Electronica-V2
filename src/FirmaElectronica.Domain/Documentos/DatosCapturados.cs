namespace FirmaElectronica.Domain.Documentos;

public sealed record SeguroCapturado(string? Aseguradora, string? Poliza, DateOnly? Inicio, DateOnly? Fin, string? Conectividad);
public sealed record DatosCapturados(string FolioControl, DateOnly? FechaPlanta, SeguroCapturado? Seguro);
