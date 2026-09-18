using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Application.Services;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Firmantes;
using FirmaElectronica.Infrastructure.Intentos;
namespace FirmaElectronica.Tests;
public class FlujoDocumentosTests : IDisposable
{
    private readonly string carpeta = Path.Combine(Path.GetTempPath(), "firma-pruebas-" + Guid.NewGuid().ToString("N"));
    private static DocumentoParaCrear Documento => new("ref", "Documento prueba", "plantilla", new Dictionary<int, string> { [1] = "Ana" });
    private GeneracionDocumentos Generacion(LegalarioFalso cliente) => new(null!, null!, null!, cliente, new RegistroIntentosArchivo(carpeta));
    [Fact]
    public async Task ConvocaConLigaDisponibleAunqueNoExistaDescargaBinaria()
    {
        var cliente = new LegalarioFalso { SoloLiga = true };
        var servicio = new ServicioConvocatoria(cliente, new QuiterFalso(), new RegistroIntentosArchivo(carpeta));
        await servicio.ConvocarAsync("doc-liga", null, [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)], "token", default);
        Assert.Equal(1, cliente.Convocatorias);
    }
    [Fact]
    public async Task NuevaSolicitudReutilizaDocumentoYExigeAutorizacionParaCambiarDatos()
    {
        var cliente = new LegalarioFalso();
        var servicio = Generacion(cliente);
        var primera = Documento with { OperacionId = Guid.NewGuid() };
        var segunda = Documento with { OperacionId = Guid.NewGuid() };
        await servicio.CrearAsync("u", primera, "token", default);
        await servicio.CrearAsync("u", segunda, "token", default);
        Assert.Equal(1, cliente.Creaciones);
        await servicio.CrearAsync("u", primera, "token", default);
        await servicio.CrearAsync("u", segunda, "token", default);
        Assert.Equal(1, cliente.Creaciones);
        var cambiada = Documento with { OperacionId = Guid.NewGuid(), Variables = new Dictionary<int, string> { [1] = "Otro dato" } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CrearAsync("u", cambiada, "token", default));
        await servicio.AutorizarNuevaGeneracionAsync("u", "ref", "plantilla", "d", default);
        await servicio.CrearAsync("u", cambiada, "token", default);
        // Las claves de operaciones anteriores siguen ligadas a su resultado original.
        await servicio.CrearAsync("u", segunda, "token", default);
        Assert.Equal(2, cliente.Creaciones);
    }
    [Fact]
    public async Task NuevaSolicitudNoDuplicaUnResultadoTodaviaIncierto()
    {
        var cliente = new LegalarioFalso { FallarCreacion = true };
        await Assert.ThrowsAsync<CreacionDocumentoException>(() => Generacion(cliente).CrearAsync("u", Documento with { OperacionId = Guid.NewGuid() }, "token", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generacion(cliente).CrearAsync("u", Documento with { OperacionId = Guid.NewGuid() }, "token", default));
        Assert.Equal(1, cliente.Creaciones);
    }
    [Fact]
    public async Task RecuperaReferenciaPorDocumentoTrasReiniciarRegistro()
    {
        var creado = await Generacion(new LegalarioFalso()).CrearAsync("u", Documento, "token", default);
        var registro = new RegistroIntentosArchivo(carpeta);
        Assert.Equal(Documento.Referencia, await registro.ReferenciaDocumentoAsync(creado.LegalarioDocumentId, default));
        Assert.Null(await registro.ReferenciaDocumentoAsync("inexistente", default));
    }
    [Fact]
    public async Task PaginaRecienteInvierteOrigenAscendenteSinCargarTodo()
    {
        var cliente = new LegalarioFalso { ListaAscendente = true };
        var consulta = new ConsultaDocumentos(cliente);
        var primera = await consulta.ConsultarAsync(["a"], 1, 15, null, "token", default);
        Assert.Equal(Enumerable.Range(23, 15).Reverse().Select(x => x.ToString()), primera.Documentos.Select(x => x.GetProperty("id").GetString()));
        Assert.Equal(3, cliente.Consultas);
        var segunda = await consulta.ConsultarAsync(["a"], 2, 15, null, "token", default);
        Assert.Equal(Enumerable.Range(8, 15).Reverse().Select(x => x.ToString()), segunda.Documentos.Select(x => x.GetProperty("id").GetString()));
    }
    [Fact]
    public async Task DosSolicitudesConcurrentesCreanUnaSolaVez()
    {
        var cliente = new LegalarioFalso();
        var resultados = await Task.WhenAll(Generacion(cliente).CrearAsync("u", Documento with { OperacionId = Guid.NewGuid() }, "token", default), Generacion(cliente).CrearAsync("u", Documento with { OperacionId = Guid.NewGuid() }, "token", default));
        Assert.Equal(1, cliente.Creaciones); Assert.Equal(resultados[0], resultados[1]);
        Assert.Equal(resultados[0], await Generacion(cliente).CrearAsync("u", Documento, "token", default));
        Assert.Equal(1, cliente.Creaciones);
    }
    [Fact]
    public async Task IntentoInciertoSeConservaTrasReconstruirServicio()
    {
        var cliente = new LegalarioFalso { FallarCreacion = true };
        await Assert.ThrowsAsync<CreacionDocumentoException>(() => Generacion(cliente).CrearAsync("u", Documento, "token", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generacion(cliente).CrearAsync("u", Documento, "token", default));
        Assert.Equal(1, cliente.Creaciones);
        var registro = new RegistroIntentosArchivo(carpeta);
        Assert.Equal("Incierto", (await registro.LeerAsync(GeneracionDocumentos.Clave("u", "ref", "plantilla"), default))!.Estado);
        Assert.DoesNotContain("token", await File.ReadAllTextAsync(Directory.GetFiles(carpeta, "*.json").Single()));
    }
    [Fact]
    public async Task ArchivoDañadoNoAutorizaOtraCreacion()
    {
        Directory.CreateDirectory(carpeta);
        await File.WriteAllTextAsync(Path.Combine(carpeta, GeneracionDocumentos.Clave("u", "ref", "plantilla") + ".json"), "dañado");
        var cliente = new LegalarioFalso();
        await Assert.ThrowsAsync<JsonException>(() => Generacion(cliente).CrearAsync("u", Documento, "token", default));
        Assert.Equal(0, cliente.Creaciones);
    }
    [Fact]
    public async Task QuiterFallidoPermiteConvocarConAvisoYNoRepiteConvocatoria()
    {
        var legalario = new LegalarioFalso();
        var servicio = new ServicioConvocatoria(legalario, new QuiterFalso(), new RegistroIntentosArchivo(carpeta));
        Firmante[] firmantes = [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)];
        var resultado = await servicio.ConvocarAsync("d", "cta", firmantes, "token", default);
        Assert.False(resultado.ContactoActualizado); Assert.NotNull(resultado.AvisoContacto); Assert.Equal(1, legalario.Convocatorias);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ConvocarAsync("d", "cta", firmantes, "token", default));
        Assert.Equal(1, legalario.Convocatorias);
    }
    [Fact]
    public async Task DocumentosSeOrdenanGlobalmenteEntrePlantillasYPaginas()
    {
        var legalario = new LegalarioFalso();
        var pagina = await new ConsultaDocumentos(legalario).ConsultarAsync(["a", "b"], 1, 2, null, "token", default);
        Assert.Equal(4, pagina.Total);
        Assert.Equal(new[] { "b2", "a2" }, pagina.Documentos.Select(d => d.GetProperty("id").GetString()));
    }
    [Fact]
    public async Task DatosModificadosExigenNuevaGeneracionExplicita()
    {
        var cliente = new LegalarioFalso();
        var servicio = Generacion(cliente);
        await servicio.CrearAsync("u", Documento, "token", default);
        var cambiado = Documento with { Variables = new Dictionary<int, string> { [1] = "Otro nombre" } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CrearAsync("u", cambiado, "token", default));
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.AutorizarNuevaGeneracionAsync("u", "ref", "plantilla", "otro-id", default));
        await servicio.AutorizarNuevaGeneracionAsync("u", "ref", "plantilla", "d", default);
        await servicio.CrearAsync("u", cambiado, "token", default);
        Assert.Equal(2, cliente.Creaciones);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(carpeta, "historial")));
    }
    [Fact]
    public async Task ResultadoInciertoNoSePuedeReiniciarSinConciliar()
    {
        var cliente = new LegalarioFalso { FallarCreacion = true };
        var servicio = Generacion(cliente);
        await Assert.ThrowsAsync<CreacionDocumentoException>(() => servicio.CrearAsync("u", Documento, "token", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.AutorizarNuevaGeneracionAsync("u", "ref", "plantilla", null, default));
        Assert.Equal(1, cliente.Creaciones);
    }
    [Fact]
    public async Task HoraDeConsultaNoCreaOtroDocumento()
    {
        var cliente = new LegalarioFalso();
        var servicio = Generacion(cliente);
        var primero = Documento with { Variables = new Dictionary<int, string> { [1] = "Ana", [2] = "10:00:00" }, PosicionHoraActual = 2 };
        var segundo = primero with { Variables = new Dictionary<int, string> { [1] = "Ana", [2] = "10:00:01" } };
        Assert.Equal(await servicio.CrearAsync("u", primero, "token", default), await servicio.CrearAsync("u", segundo, "token", default));
        Assert.Equal(1, cliente.Creaciones);
    }
    [Fact]
    public async Task RecuperacionRevisaMasDeCienDocumentosYConservaSoloCoincidenciasDelIntento()
    {
        var fecha = DateTimeOffset.Parse("2026-09-15T12:00:00Z");
        var registro = new RegistroIntentosArchivo(carpeta);
        await registro.GuardarAsync(new(GeneracionDocumentos.Clave("u", "22387933", "p"), "huella", "22387933", "Documentación_BENJAMIN_3N1CK3CE4TL210692", "p", fecha, "Incierto", null), default);
        var cliente = new LegalarioFalso
        {
            ListaRecuperacion = Enumerable.Range(1, 102).Select(x => JsonSerializer.SerializeToElement(new
            {
                id = x.ToString(),
                name = x >= 101 ? "Documentacion_BENJAMIN_3N1CK3CE4TL210692" : "Otro expediente",
                created_at = x == 102 ? fecha.AddDays(-1) : fecha.AddMinutes(1)
            })).ToArray()
        };
        var servicio = new ConciliacionDocumentos(registro, cliente);
        var candidatos = await servicio.CandidatosAsync(GeneracionDocumentos.Clave("u", "22387933", "p"), "token", default);
        Assert.Equal(2, cliente.Consultas);
        Assert.Equal(new[] { "101", "102" }, candidatos.Select(d => d.GetProperty("id").GetString()).Order());
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ConfirmarAsync(GeneracionDocumentos.Clave("u", "22387933", "p"), "99", "token", default));
        var recuperado = await servicio.ConfirmarAsync(GeneracionDocumentos.Clave("u", "22387933", "p"), "101", "token", default);
        Assert.Equal("101", recuperado.LegalarioDocumentId);
        Assert.Equal(0, cliente.Creaciones);
    }
    [Fact]
    public async Task TiempoAgotadoDeQuiterNoImpideInvitarNiPermiteDuplicar()
    {
        var cliente = new LegalarioFalso();
        var servicio = new ServicioConvocatoria(cliente, new QuiterAgotado(), new RegistroIntentosArchivo(carpeta));
        Firmante[] firmantes = [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)];
        var resultado = await servicio.ConvocarAsync("d", "cuenta", firmantes, "token", default);
        Assert.False(resultado.ContactoActualizado);
        Assert.NotNull(resultado.AvisoContacto);
        Assert.Equal(1, cliente.Convocatorias);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ConvocarAsync("d", "cuenta", firmantes, "token", default));
        Assert.Equal(1, cliente.Convocatorias);
    }
    [Fact]
    public async Task EsperaRepositorioDeFirmantesSinExigirLigaOPdf()
    {
        var cliente = new LegalarioFalso { ConsultasPendientes = 1, SinDescarga = true };
        var servicio = new ServicioConvocatoria(cliente, new QuiterFalso(), new RegistroIntentosArchivo(carpeta));
        await servicio.ConvocarAsync("d", null, [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)], "token", default);
        Assert.Equal(2, cliente.ConsultasFirmas);
        Assert.Equal(1, cliente.Convocatorias);
    }
    [Theory]
    [InlineData(false, "Rechazado")]
    [InlineData(true, "RechazadoTemporal")]
    public async Task RechazoConfirmadoPermiteCorregirYReintentar(bool temporal, string estado)
    {
        var registro = new RegistroIntentosArchivo(carpeta);
        var cliente = new LegalarioFalso { ErrorConvocatoria = new("Rechazado", 422, reintentable: temporal) };
        var servicio = new ServicioConvocatoria(cliente, new QuiterFalso(), registro);
        Firmante[] firmantes = [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)];
        await Assert.ThrowsAsync<OperacionLegalarioException>(() => servicio.ConvocarAsync("d", null, firmantes, "token", default));
        Assert.Equal(estado, (await registro.LeerAsync(GeneracionDocumentos.Clave("CONVOCATORIA", "d", "FIRMANTES"), default))!.Estado);
        cliente.ErrorConvocatoria = null;
        await servicio.ConvocarAsync("d", null, firmantes, "token", default);
        Assert.Equal(2, cliente.Convocatorias);
    }
    [Theory]
    [InlineData("EnCurso")]
    [InlineData("Incierto")]
    public async Task ConvocatoriaInciertaSeConciliaSinReenviarAunquePrimeroNoAparezcanFirmantes(string estado)
    {
        var registro = new RegistroIntentosArchivo(carpeta);
        var clave = GeneracionDocumentos.Clave("CONVOCATORIA", "d", "FIRMANTES");
        await registro.GuardarAsync(new(clave, "", "d", "Convocatoria", "FIRMANTES", DateTimeOffset.UtcNow, estado, null), default);
        var cliente = new LegalarioFalso();
        var servicio = new ServicioConvocatoria(cliente, new QuiterFalso(), registro);
        Firmante[] firmantes = [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)];
        var error = await Assert.ThrowsAsync<OperacionLegalarioException>(() => servicio.ConvocarAsync("d", null, firmantes, "token", default));
        Assert.True(error.ResultadoIncierto);
        Assert.False(error.Reintentable);
        cliente.FirmantesExistentes = 1;
        var recuperada = await servicio.ConvocarAsync("d", null, firmantes, "token", default);
        Assert.True(recuperada.Recuperada);
        Assert.Equal("Conciliado", (await registro.LeerAsync(clave, default))!.Estado);
        Assert.Equal(0, cliente.Convocatorias);
    }
    [Fact]
    public async Task PostInciertoQuedaPersistidoYNoSeRepite()
    {
        var cliente = new LegalarioFalso { ErrorConvocatoria = new("Timeout", resultadoIncierto: true) };
        var registro = new RegistroIntentosArchivo(carpeta);
        var servicio = new ServicioConvocatoria(cliente, new QuiterFalso(), registro);
        Firmante[] firmantes = [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)];
        await Assert.ThrowsAsync<OperacionLegalarioException>(() => servicio.ConvocarAsync("d", null, firmantes, "token", default));
        Assert.Equal("Incierto", (await registro.LeerAsync(GeneracionDocumentos.Clave("CONVOCATORIA", "d", "FIRMANTES"), default))!.Estado);
        cliente.ErrorConvocatoria = null;
        await Assert.ThrowsAsync<OperacionLegalarioException>(() => servicio.ConvocarAsync("d", null, firmantes, "token", default));
        Assert.Equal(1, cliente.Convocatorias);
    }
    [Fact]
    public async Task CancelarEsperaNoRegistraNiEnviaConvocatoria()
    {
        var cliente = new LegalarioFalso { ConsultasPendientes = 10 };
        var registro = new RegistroIntentosArchivo(carpeta);
        var servicio = new ServicioConvocatoria(cliente, new QuiterFalso(), registro);
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servicio.ConvocarAsync("d", null, [new("Ana", "ana@example.com", "5512345678", TipoFirmante.Cliente)], "token", cancelacion.Token));
        Assert.Null(await registro.LeerAsync(GeneracionDocumentos.Clave("CONVOCATORIA", "d", "FIRMANTES"), default));
        Assert.Equal(0, cliente.Convocatorias);
    }
    private sealed class QuiterAgotado : IQuiterClient
    {
        public Task ActualizarContactoClienteAsync(ContactoClienteQuiter contacto, CancellationToken ct)
        {
            Assert.True(ct.CanBeCanceled);
            throw new OperationCanceledException("Tiempo de espera simulado");
        }
    }
    private sealed class QuiterFalso : IQuiterClient
    { public Task ActualizarContactoClienteAsync(ContactoClienteQuiter contacto, CancellationToken cancellationToken) => throw new InvalidOperationException("Error simulado"); }
    private sealed class LegalarioFalso : ILegalarioClient
    {
        public int Creaciones, Convocatorias, Consultas;
        public bool SoloLiga, SinDescarga;
        public int ConsultasPendientes, ConsultasFirmas, FirmantesExistentes;
        public OperacionLegalarioException? ErrorConvocatoria;
        public Task<string?> ObtenerUrlDocumentoAsync(string documentoId, string token, CancellationToken ct) => Task.FromResult<string?>(SoloLiga ? "https://example.com/documento.pdf" : null);
        public JsonElement[]? ListaRecuperacion;
        public bool ListaAscendente;
        public bool FallarCreacion;
        public async Task<DocumentoGenerado> CrearDocumentoAsync(DocumentoParaCrear documento, string token, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Creaciones); await Task.Delay(50, cancellationToken);
            if (FallarCreacion) throw new CreacionDocumentoException("Incierto", true);
            return new(documento.Referencia, documento.Nombre, "d", DateTimeOffset.UtcNow);
        }
        public Task<PaginaLegalario> ConsultarPaginaAsync(string plantilla, int pagina, int cantidad, string? busqueda, string token, CancellationToken ct)
        {
            Consultas++;
            if (ListaRecuperacion is not null)
            {
                Assert.Equal("3N1CK3CE4TL210692", busqueda);
                return Task.FromResult(new PaginaLegalario(ListaRecuperacion.Skip((pagina - 1) * cantidad).Take(cantidad).ToArray(), (ListaRecuperacion.Length + cantidad - 1) / cantidad, ListaRecuperacion.Length));
            }
            if (ListaAscendente) return Task.FromResult(new PaginaLegalario(Enumerable.Range(1, 37).Skip((pagina - 1) * cantidad).Take(cantidad).Select(x => JsonSerializer.SerializeToElement(new { id = x.ToString(), created_at = new DateTime(2026, 1, 1).AddDays(x) })).ToArray(), 3, 37));
            return Task.FromResult(new PaginaLegalario(
            [JsonSerializer.SerializeToElement(new { id = plantilla + pagina, created_at = $"2026-09-{(pagina == 1 ? 1 : plantilla == "b" ? 8 : 7):00}T12:00:00Z" })], 2, 2)); 
        }
        public Task<JsonElement> ConsultarDocumentoAsync(string documentoId, string token, CancellationToken ct) => Task.FromResult(JsonSerializer.SerializeToElement(new { id = documentoId, name = documentoId == "101" ? "Documentacion_BENJAMIN_3N1CK3CE4TL210692" : "Otro", template_id = "p", created_at = "2026-09-15T12:00:00Z" }));
        public Task<byte[]?> DescargarPdfAsync(string documentoId, string token, CancellationToken ct) => Task.FromResult<byte[]?>(SoloLiga || SinDescarga ? null : "%PDF-1.7"u8.ToArray());
        public Task<EstadoFirmas> ConsultarFirmasAsync(string documentoId, string token, CancellationToken ct)
        {
            ConsultasFirmas++;
            if (ConsultasPendientes-- > 0) throw new OperacionLegalarioException("Repositorio pendiente", 404, reintentable: true);
            return Task.FromResult(new EstadoFirmas(0, FirmantesExistentes, []));
        }
        public Task ConvocarFirmantesAsync(string documentoId, IReadOnlyCollection<Firmante> firmantes, string token, CancellationToken ct)
        {
            Convocatorias++;
            if (ErrorConvocatoria is not null) throw ErrorConvocatoria;
            return Task.CompletedTask;
        }
        public Task ReenviarInvitacionAsync(string firmanteId, string token, CancellationToken ct) => throw new NotImplementedException();
        public Task EliminarDocumentoAsync(string documentoId, string token, CancellationToken ct) => throw new NotImplementedException();
    }
    public void Dispose() { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); }
}
