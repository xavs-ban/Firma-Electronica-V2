using System.Net;

namespace FirmaElectronica.Application.Abstractions;

public sealed class CreacionDocumentoException(
    string mensaje, bool resultadoIncierto, HttpStatusCode? estadoHttp = null) : Exception(mensaje)
{
    // No autoriza un reintento: primero hay que consultar si el documento existe.
    public bool ResultadoIncierto { get; } = resultadoIncierto;
    public HttpStatusCode? EstadoHttp { get; } = estadoHttp;
}
