namespace FirmaElectronica.Domain.Documentos;

// La capa de aplicación debe resolver la plantilla y preparar sus variables antes del envío.
public sealed record DocumentoParaCrear(
    string Referencia,
    string Nombre,
    string PlantillaId,
    IReadOnlyDictionary<int, string> Variables)
{
    public Guid? OperacionId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int? PosicionHoraActual { get; init; }
}
