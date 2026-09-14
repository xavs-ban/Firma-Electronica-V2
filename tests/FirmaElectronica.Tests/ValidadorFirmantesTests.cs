using FirmaElectronica.Domain.Firmantes;
namespace FirmaElectronica.Tests;
public class ValidadorFirmantesTests
{
    [Theory]
    [InlineData("551234567")][InlineData("55123456789")][InlineData("55abc45678")][InlineData("+525512345678")][InlineData("55 1234 5678")]
    public void RechazaTelefonoSinDiezDigitosExactos(string telefono) =>
        Assert.Throws<ArgumentException>(() => ValidadorFirmantes.Validar([new("Ana", "ana@example.com", telefono, TipoFirmante.Cliente)]));
    [Theory]
    [InlineData("ana@empresa")][InlineData("ana..p@empresa.com")][InlineData("ana@-empresa.com")][InlineData("ana empresa.com")]
    public void RechazaCorreoInvalido(string correo) =>
        Assert.Throws<ArgumentException>(() => ValidadorFirmantes.Validar([new("Ana", correo, "5512345678", TipoFirmante.Cliente)]));
    [Fact]
    public void AceptaContactosValidos() => ValidadorFirmantes.Validar([new("Ana", "ana.p+firma@empresa.com.mx", "5512345678", TipoFirmante.Cliente)]);
}
