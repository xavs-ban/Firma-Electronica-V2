using System.Net;

namespace FirmaElectronica.Application.Abstractions;

public sealed class CreacionDocumentoException(
    string mensaje, bool resultadoIncierto, HttpStatusCode? estadoHttp = null) : Exception(mensaje)
{
    // Identifica respuestas que no confirman si Legalario procesó la creación.
    public bool ResultadoIncierto { get; } = resultadoIncierto;
    public HttpStatusCode? EstadoHttp { get; } = estadoHttp;
}
