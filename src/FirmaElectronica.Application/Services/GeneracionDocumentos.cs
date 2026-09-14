using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Usuarios;
namespace FirmaElectronica.Application.Services;
public sealed record PreparacionDocumento(DocumentoParaCrear Documento, IReadOnlyDictionary<string, object?> Datos);
public sealed class GeneracionDocumentos(IReferenciaDataProvider referencias, IPlantillaResolver plantillas,
    PreparadorVariables variables, ICreadorDocumentoLegalario creador, IRegistroIntentos registro)
{
    public async Task<PreparacionDocumento> PrepararAsync(UsuarioFirma usuario, SolicitudDocumento solicitud, DatosCapturados captura, CancellationToken ct)
    {
        usuario.ValidarAgencia(solicitud.Agencia);
        var datos = await referencias.ObtenerDatosReferenciaAsync(solicitud.Referencia, ct);
        var agencia = PreparadorVariables.Texto(datos, "dealer").Trim().ToUpperInvariant();
        usuario.ValidarAgencia(agencia);
        if (!agencia.Equals(solicitud.Agencia.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La referencia pertenece a otra agencia.");
        if (PreparadorVariables.Texto(datos, "tipoExpediente") != "SEMINUEVO" &&
            captura.FolioControl.Any(c => c < '0' || c > '9'))
            throw new ArgumentException("El folio de control sólo acepta números.");
        var regla = plantillas.Resolver(solicitud, datos);
        return new(variables.Preparar(solicitud, regla, datos, captura, DateTimeOffset.UtcNow), datos);
    }
    public static string Clave(string usuario, string referencia, string plantilla) => Hash(JsonSerializer.Serialize(new[] { usuario, referencia.Trim(), plantilla }));
    private static string Hash(string texto) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    public Task<bool> AutorizarNuevaGeneracionAsync(string usuario, string referencia, string plantilla, string? documentoAnterior, CancellationToken ct)
    {
        var clave = Clave(usuario, referencia, plantilla);
        return registro.ExclusivoAsync(clave, async cancelacion =>
        {
            var intento = await registro.LeerAsync(clave, cancelacion) ?? throw new KeyNotFoundException("No existe el intento.");
            if (intento.Estado is not ("Confirmado" or "Conciliado" or "Rechazado"))
                throw new InvalidOperationException("Primero debe conciliar el resultado incierto.");
            if (intento.Documento?.LegalarioDocumentId != documentoAnterior)
                throw new ArgumentException("Confirme el identificador del documento anterior.");
            await registro.GuardarAsync(intento with { Estado = "NuevoAutorizado", Documento = null }, cancelacion);
            return true;
        }, ct);
    }
    public Task<DocumentoGenerado> CrearAsync(string usuario, DocumentoParaCrear documento, string token, CancellationToken ct)
    {
        var clave = Clave(usuario, documento.Referencia, documento.PlantillaId);
        var huella = Hash(JsonSerializer.Serialize(new
        {
            documento.Referencia, documento.Nombre, documento.PlantillaId,
            Variables = documento.Variables.Where(v => v.Key != documento.PosicionHoraActual).OrderBy(v => v.Key).ToArray()
        }));
        return registro.ExclusivoAsync(clave, async cancelacion =>
        {
            var claveOperacion = documento.OperacionId is { } operacion ? Clave(usuario, operacion.ToString(), "OPERACION") : null;
            if (claveOperacion is not null)
            {
                var repetida = await registro.LeerAsync(claveOperacion, cancelacion);
                if (repetida is not null)
                {
                    if (repetida.Huella != huella) throw new ArgumentException("La solicitud ya fue utilizada con otros datos.");
                    if (repetida.Documento is not null) return repetida.Documento;
                    throw new InvalidOperationException("Esta solicitud ya fue procesada. Consulta su estado antes de repetirla.");
                }
            }
            async Task Guardar(IntentoDocumento intento, CancellationToken token)
            {
                await registro.GuardarAsync(intento, token);
                if (claveOperacion is not null) await registro.GuardarAsync(intento with { Clave = claveOperacion }, token);
            }
            var existente = await registro.LeerAsync(clave, cancelacion);
            if (existente?.Documento is not null && claveOperacion is null)
            {
                if (existente.Huella != huella) throw new InvalidOperationException("La referencia ya tiene un documento con otros datos. Confirme una nueva generación antes de continuar.");
                return existente.Documento;
            }
            if (existente is not null && existente.Documento is null && existente.Estado is not ("NuevoAutorizado" or "Rechazado")) throw new InvalidOperationException("Ya existe un intento para esta referencia y plantilla. Consulte y concilie el resultado antes de generar otra vez.");
            var intento = new IntentoDocumento(clave, huella, documento.Referencia, documento.Nombre, documento.PlantillaId, DateTimeOffset.UtcNow, "EnCurso", null);
            await Guardar(intento, cancelacion);
            try
            {
                var creado = await creador.CrearDocumentoAsync(documento, token, cancelacion);
                await Guardar(intento with { Estado = "Confirmado", Documento = creado }, CancellationToken.None);
                return creado;
            }
            catch (CreacionDocumentoException e)
            {
                await Guardar(intento with { Estado = e.ResultadoIncierto ? "Incierto" : "Rechazado" }, CancellationToken.None);
                throw;
            }
            // Si el proceso cae o falla la persistencia, EnCurso también bloquea un segundo envío.
        }, ct);
    }
}
