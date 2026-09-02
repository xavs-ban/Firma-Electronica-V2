using FirmaElectronica.Domain.Variables;

namespace FirmaElectronica.Tests;

public sealed class CatalogoVariablesTests
{
    [Fact]
    public void HyundaiMantieneVariables105A110EnOrdenCorrecto()
    {
        Assert.Equal("id_cliente", CatalogoVariables.HyundaiPersonaFisica[104]);
        Assert.Equal("estado_civil", CatalogoVariables.HyundaiPersonaFisica[105]);
        Assert.Equal("fecha_pedido", CatalogoVariables.HyundaiPersonaFisica[106]);
        Assert.Equal("tipo_ven_dest", CatalogoVariables.HyundaiPersonaFisica[107]);
        Assert.Equal("tipo_ped", CatalogoVariables.HyundaiPersonaFisica[108]);
        Assert.Equal("num_pedimento", CatalogoVariables.HyundaiPersonaFisica[109]);
    }

    [Fact]
    public void HyundaiIdClienteSaleDeCtaCliente()
    {
        Assert.Equal("cta_cliente", CatalogoVariables.AliasHyundai["id_cliente"]);
    }

    [Fact]
    public void HyundaiTipoVentaDestinoUsaCampoDelSpYNoDescripcion()
    {
        Assert.Contains("TIPO_VEN_DEST", CatalogoVariables.FallbacksHyundai["tipo_ven_dest"]);
        Assert.DoesNotContain("desc_tipo_venta", CatalogoVariables.FallbacksHyundai["tipo_ven_dest"]);
        Assert.DoesNotContain("desc_tventa", CatalogoVariables.FallbacksHyundai["tipo_ven_dest"]);
    }

    [Fact]
    public void PersonaFisicaMantieneCantidadLegacy()
    {
        Assert.Equal(104, CatalogoVariables.PersonaFisica.Count);
    }

    [Fact]
    public void PersonaMoralMantieneFallbackRepresentanteLegalConApoderado()
    {
        Assert.Equal("nombre_ap", CatalogoVariables.FallbacksRepresentanteLegal["nombre_rl"]);
        Assert.Equal("num_iden_ap", CatalogoVariables.FallbacksRepresentanteLegal["num_iden_rl"]);
    }
}
