using FirmaElectronica.Application.Services;
using FirmaElectronica.Domain.Documentos;
using FirmaElectronica.Domain.Plantillas;

namespace FirmaElectronica.Tests;

public sealed class PlantillaResolverTests
{
    [Fact]
    public void SanManuelFinanciamientoUsaPlantillaActualYSinRepresentanteLegal()
    {
        var resolver = new PlantillaResolver();
        var solicitud = new SolicitudDocumento("22387011", "475", "CON", DateOnly.FromDateTime(DateTime.Today), AplicaSeguro: false);

        var regla = resolver.Resolver(solicitud, new Dictionary<string, object?>
        {
            ["tipo_de_venta"] = "1C",
            ["isPersonaMoral"] = false,
            ["isSeminuevo"] = false
        });

        Assert.Equal(TipoPlantilla.Financiamiento, regla.Tipo);
        Assert.Equal("6761df30d1638b468a0ce972", regla.LegalarioTemplateId);
        Assert.False(regla.IncluyeRepresentanteLegal);
    }

    [Fact]
    public void HyundaiPachucaUsaPlantillaHyundaiYRepresentanteLegal()
    {
        var resolver = new PlantillaResolver();
        var solicitud = new SolicitudDocumento("21773530", "B20ABPA002", "CON", DateOnly.FromDateTime(DateTime.Today), AplicaSeguro: false);

        var regla = resolver.Resolver(solicitud, new Dictionary<string, object?>
        {
            ["tipo_de_venta"] = "1C",
            ["isPersonaMoral"] = false,
            ["isSeminuevo"] = false
        });

        Assert.Equal(TipoPlantilla.Hyundai, regla.Tipo);
        Assert.Equal("6a7e24724fdc482a902a91d2", regla.LegalarioTemplateId);
        Assert.True(regla.IncluyeRepresentanteLegal);
    }

    [Theory]
    [InlineData("1", TipoPlantilla.Contado)]
    [InlineData("1F", TipoPlantilla.Contado)]
    [InlineData("1C", TipoPlantilla.Financiamiento)]
    [InlineData("1S", TipoPlantilla.Financiamiento)]
    [InlineData("", TipoPlantilla.Contado)]
    public void TipoDeVentaDelSpDefinePlantilla(string tipoSp, TipoPlantilla esperado)
    {
        Assert.Equal(esperado, PlantillaResolver.ResolverTipoDesdeSp(tipoSp));
    }
}
