using FirmaElectronica.Application.Services;
namespace FirmaElectronica.Tests;
public class IdentidadDocumentoTests
{
    [Theory]
    [InlineData("Documentacion_MARIA_P123", true)]
    [InlineData("Documentación_MARÍA_P123", true)]
    [InlineData("Documentacion_MARIA_P124", false)]
    [InlineData("Documentacion_OTRA_P123", false)]
    [InlineData("", false)]
    public void ToleraAcentosPeroRechazaOtroClienteOVin(string recibido, bool esperado)
    {
        Assert.Equal(esperado, IdentidadDocumento.Coincide(recibido, "Documentación_MARÍA_P123"));
    }
}
