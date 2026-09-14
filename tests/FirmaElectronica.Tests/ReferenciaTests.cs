using FirmaElectronica.Application.Abstractions;
using FirmaElectronica.Application.Services;
namespace FirmaElectronica.Tests;
public class ReferenciaTests
{
    [Theory]
    [InlineData(0, "PERSONA_MORAL")][InlineData(1, "PERSONA_FISICA")][InlineData(2, "SEMINUEVO")]
    public async Task RespetaPrecedenciaYDetieneBusqueda(int encontradoEn, string tipo)
    {
        var consulta = new Consulta(encontradoEn, 1);
        var datos = await new ProveedorReferencias(consulta).ObtenerDatosReferenciaAsync(" 001234 ", default);
        Assert.Equal(tipo, datos["tipoExpediente"]);
        Assert.Equal(encontradoEn + 1, consulta.Llamadas.Count);
        Assert.Equal("sp_ObtenerDatosReferenciaPM", consulta.Llamadas[0]);
        Assert.Equal("001234", consulta.Referencia);
    }
    [Fact]
    public async Task DuplicadosNoContinuanAlSiguienteSp()
    {
        var consulta = new Consulta(0, 2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProveedorReferencias(consulta).ObtenerDatosReferenciaAsync("001234", default));
        Assert.Single(consulta.Llamadas);
    }
    [Fact]
    public async Task ReferenciaNoEncontradaNoSeConfundeConDatosValidos()
    {
        var consulta = new Consulta(4, 1);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ProveedorReferencias(consulta).ObtenerDatosReferenciaAsync("001234", default));
        Assert.Equal(3, consulta.Llamadas.Count);
    }
    [Theory]
    [InlineData("12ABC")][InlineData("1.2")][InlineData("-12")]
    public async Task RechazaReferenciaNoNumericaAntesDeConsultar(string referencia)
    {
        var consulta = new Consulta(0, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => new ProveedorReferencias(consulta).ObtenerDatosReferenciaAsync(referencia, default));
        Assert.Empty(consulta.Llamadas);
    }
    private sealed class Consulta(int encontradoEn, int filas) : IConsultaReferencia
    {
        public List<string> Llamadas { get; } = [];
        public string? Referencia { get; private set; }
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ConsultarAsync(string procedimiento, string referencia, CancellationToken cancellationToken)
        {
            Referencia = referencia; Llamadas.Add(procedimiento);
            IReadOnlyList<IReadOnlyDictionary<string, object?>> resultado = Llamadas.Count == encontradoEn + 1
                ? Enumerable.Range(0, filas).Select(_ => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["dealer"] = "306" }).ToArray() : [];
            return Task.FromResult(resultado);
        }
    }
}
