using FirmaElectronica.Application.Services;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Plantillas;
using FirmaElectronica.Domain.Usuarios;
using FirmaElectronica.Domain.Variables;
namespace FirmaElectronica.Tests;
public class PreparacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 8, 18, 0, 0, TimeSpan.Zero);
    private static DocumentoParaCrear Preparar(TipoPlantilla tipo, Dictionary<string, object?> datos, string venta = "CON", bool aplica = false, SeguroCapturado? seguro = null) =>
        new PreparadorVariables().Preparar(new("ref", "306", venta, new(2026, 9, 8), aplica), new("306", tipo, "plantilla", true), datos, new("F-123", new(2026, 8, 9), seguro), Ahora);
    [Fact]
    public void FinanciamientoAgregaVariables105A108SinMoverLasAnteriores()
    {
        var datos = new Dictionary<string, object?> { ["pais_nacimiento"] = "MEXICO", ["numero_exterior"] = "123", ["nacional"] = "X", ["extranjero"] = "" };
        var contado = Preparar(TipoPlantilla.Contado, datos);
        var financiamiento = Preparar(TipoPlantilla.Financiamiento, datos);
        Assert.Equal(108, financiamiento.Variables.Count);
        for (var i = 1; i <= 104; i++) Assert.Equal(contado.Variables[i], financiamiento.Variables[i]);
        Assert.Equal(new[] { "MEXICO", "123", "X", "" }, Enumerable.Range(105, 4).Select(i => financiamiento.Variables[i]));
    }
    [Theory]
    [InlineData("AXA SEGUROS")][InlineData("CUENTA CLIENTE")]
    public void AseguradoraCapturadaLlegaALaPosicionLegacy(string aseguradora)
    {
        var documento = Preparar(TipoPlantilla.Contado, new(), "CON", true, new(aseguradora, "POL", null, null, "No aplica"));
        Assert.Equal(aseguradora, documento.Variables[77]);
        Assert.Equal("POL", documento.Variables[78]);
        Assert.Equal("", documento.Variables[79]);
    }
    [Fact]
    public void FinanciamientoSinSeguroIndicaNoComproSinMoverVariables()
    {
        var documento = Preparar(TipoPlantilla.Financiamiento, new(), "RCI");
        Assert.Equal("No compró seguro", documento.Variables[77]);
        foreach (var i in Enumerable.Range(78, 3)) Assert.Equal("", documento.Variables[i]);
    }
    [Theory]
    [InlineData("AFD")]
    [InlineData("CON")]
    [InlineData("DEM")]
    [InlineData("LOC")]
    [InlineData("NMX")]
    [InlineData("PET")]
    [InlineData("RCI")]
    [InlineData("SIC")]
    [InlineData("TUA")]
    [InlineData("OTR")]
    public void TipoVentaConservaIdentificadorEnLaPlantilla(string identificador)
    {
        foreach (var tipo in new[] { TipoPlantilla.Contado, TipoPlantilla.Financiamiento, TipoPlantilla.PersonaMoral, TipoPlantilla.Hyundai })
        {
            var catalogo = tipo == TipoPlantilla.PersonaMoral ? CatalogoVariables.PersonaMoral :
                tipo == TipoPlantilla.Hyundai ? CatalogoVariables.HyundaiPersonaFisica : CatalogoVariables.PersonaFisica;
            var posicion = catalogo.ToList().IndexOf("tipo_venta") + 1;
            Assert.True(posicion > 0);
            Assert.Equal(identificador, Preparar(tipo, new(), identificador).Variables[posicion]);
        }
    }
    [Fact]
    public void PersonaFisicaConservaPosicionesYFormato()
    {
        var documento = Preparar(TipoPlantilla.Contado, new() { ["NOMBRE"] = "ANA", ["nombre_completo"] = "ANA PEREZ", ["vin"] = "VIN1", ["total_factura"] = 123456.78m, ["accesorios"] = " tapetes, , alarma ", ["hora_entrega"] = "9:05AM" });
        Assert.Equal("Documentación_ANA PEREZ_VIN1", documento.Nombre);
        Assert.Equal(104, documento.Variables.Count);
        Assert.Equal("ANA", documento.Variables[1]);
        Assert.Equal("123,456.78", documento.Variables[45]);
        Assert.Equal("09", documento.Variables[61]);
        Assert.Equal("08", documento.Variables[62]);
        Assert.Equal("2026", documento.Variables[63]);
        Assert.Equal("09:05 AM", documento.Variables[52]);
        Assert.Equal("12:00:00", documento.Variables[69]);
        Assert.Equal("F-123", documento.Variables[68]);
        Assert.Equal("tapetes", documento.Variables[95]);
        Assert.Equal("alarma", documento.Variables[96]);
        Assert.Equal("", documento.Variables[97]);
    }
    [Fact]
    public void SeminuevosIntercambiaCapacidadYFechaSinModificarCatalogo()
    {
        var datos = new Dictionary<string, object?> { ["capacidad"] = "5", ["fecha_de_entrega"] = "2026-09-10" };
        var semi = Preparar(TipoPlantilla.SeminuevosContado, datos);
        var nuevo = Preparar(TipoPlantilla.Contado, datos);
        Assert.Equal("2026-09-10", semi.Variables[41]); Assert.Equal("5", semi.Variables[42]);
        Assert.Equal("5", nuevo.Variables[41]); Assert.Equal("2026-09-10", nuevo.Variables[42]);
    }
    [Fact]
    public void HyundaiResuelveAliasYConservaHuecos()
    {
        var documento = Preparar(TipoPlantilla.Hyundai, new() { ["Nombre"] = "LUIS", ["CTA_CLIENTE"] = "C55", ["TIPO_VENT_DES"] = "DESTINO", ["TIPO_PEDIDO"] = "PEDIDO", ["num_pedimento"] = "P1", ["accesorios"] = string.Join(',', Enumerable.Range(1, 12)) });
        Assert.Equal(110, documento.Variables.Count);
        Assert.Equal("LUIS", documento.Variables[1]);
        Assert.Equal("", documento.Variables[93]);
        Assert.Equal("11", documento.Variables[103]);
        Assert.Equal("12", documento.Variables[104]);
        Assert.Equal("C55", documento.Variables[105]);
        Assert.Equal("DESTINO", documento.Variables[108]);
        Assert.Equal("PEDIDO", documento.Variables[109]);
        Assert.Equal("P1", documento.Variables[110]);
    }
    [Fact]
    public void MoralUsaApoderadoComoRespaldoYMantieneAccesoriosSieteSeis()
    {
        var documento = Preparar(TipoPlantilla.PersonaMoral, new() { ["nombre_ap"] = "ANA", ["apaterno_ap"] = "PEREZ", ["nombre_rl"] = "", ["accesorios"] = "1,2,3,4,5,6,7,8" });
        int Posicion(string clave) => CatalogoVariables.PersonaMoral.ToList().IndexOf(clave) + 1;
        Assert.Equal("ANA", documento.Variables[Posicion("nombre_rl")]);
        Assert.Equal("ANA PEREZ", documento.Variables[Posicion("nombre_completo_rl")]);
        Assert.Equal("7", documento.Variables[Posicion("accesorio_07")]);
        Assert.Equal("6", documento.Variables[Posicion("accesorio_06")]);
        Assert.True(Posicion("accesorio_07") < Posicion("accesorio_06"));
    }
    [Theory]
    [InlineData("CON")][InlineData("TUA")][InlineData("OTR")][InlineData("SIC")]
    public void ContadoSinSeguroMantieneConectividad(string venta)
    {
        var documento = Preparar(TipoPlantilla.Contado, new(), venta, false, new("IGNORAR", "P", null, null, "Conectividad"));
        Assert.Equal("No compró seguro", documento.Variables[77]);
        Assert.Equal("", documento.Variables[78]); Assert.Equal("", documento.Variables[79]);
        Assert.Equal("Conectividad", documento.Variables[80]);
    }
    [Fact]
    public void FinanciamientoUsaSeguroDelSp()
    {
        var documento = Preparar(TipoPlantilla.Financiamiento, new() { ["poliza"] = "POL1", ["cod_aseguradora"] = 2124, ["fecha_inicio_seguro"] = "2026-01-10", ["fecha_fin_seguro"] = "2027-07-10" }, "FIN");
        Assert.Equal("QUALITAS COMPAÑÍA DE SEGUROS", documento.Variables[77]);
        Assert.Equal("18 mes(es) (1 año(s))", documento.Variables[79]);
    }
    [Fact]
    public void NoEnviaImporteInvalido()
    { Assert.Throws<ArgumentException>(() => Preparar(TipoPlantilla.Contado, new() { ["total_factura"] = "ERROR" })); }
    [Theory]
    [InlineData(100, "CIENTO 00/100 MXN")]
    [InlineData(1000, "UN MIL 00/100 MXN")]
    [InlineData(1000000, "UN MILLONES 00/100 MXN")]
    public void MantieneRedaccionActualDelImporte(int numero, string esperado) => Assert.Equal(esperado, ImporteEnLetras.Convertir(numero));
    [Theory]
    [InlineData(TipoPlantilla.Contado, true, 4)]
    [InlineData(TipoPlantilla.Financiamiento, false, 3)]
    [InlineData(TipoPlantilla.Hyundai, false, 3)]
    [InlineData(TipoPlantilla.Hyundai, true, 4)]
    public void FirmantesRespetanReglaDePlantilla(TipoPlantilla tipo, bool incluir, int total)
    {
        var preparador = new PreparadorFirmantes(new() { IncluirRepresentanteHyundai = incluir });
        var lista = preparador.Preparar(new("306", tipo, "plantilla", incluir), new Dictionary<string, object?>());
        Assert.Equal(total, lista.Count);
        Assert.Equal(incluir, lista.Any(f => f.TipoFirmante == FirmaElectronica.Domain.Firmantes.TipoFirmante.RepresentanteLegal));
    }
    [Fact]
    public void HyundaiIncluyeRepresentantePorDefecto()
    {
        var firmantes = new PreparadorFirmantes(new()).Preparar(new("B20ABMS009", TipoPlantilla.Hyundai, "plantilla", true), new Dictionary<string, object?>());
        Assert.Equal(4, firmantes.Count);
        Assert.Contains(FirmaElectronica.Domain.Firmantes.CatalogoGerentes.RepresentanteLegal, firmantes);
    }
    [Fact]
    public void ConsultaIncluyeLasMismasPlantillasDeRespaldoQueGeneracion()
    {
        var permitidas = PlantillasPorAgencia.Obtener("528");
        var resolver = new PlantillaResolver();
        var solicitud = new SolicitudDocumento("ref", "528", "CON", new(2026, 9, 8), false);
        var moral = resolver.Resolver(solicitud, new Dictionary<string, object?> { ["isPersonaMoral"] = true });
        var semi = resolver.Resolver(solicitud, new Dictionary<string, object?> { ["isSeminuevo"] = true });
        Assert.Equal(moral.LegalarioTemplateId, permitidas[TipoPlantilla.PersonaMoral]);
        Assert.Equal(semi.LegalarioTemplateId, permitidas[TipoPlantilla.SeminuevosContado]);
        Assert.DoesNotContain(TipoPlantilla.Contado, PlantillasPorAgencia.Obtener("B20ABMS009").Keys);
    }
    [Fact]
    public void PermisosAgenciasAceptanSeparadoresYRechazanOtraAgencia()
    {
        var usuario = new UsuarioFirma("u", "U", "rol", "306; 474,457");
        usuario.ValidarAgencia("474");
        Assert.Throws<UnauthorizedAccessException>(() => usuario.ValidarAgencia("528"));
    }
}
