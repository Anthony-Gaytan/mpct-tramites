using MpctTramites.Api.Services;

namespace MpctTramites.Api.Tests;

public sealed class SunatValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("2012345678")]
    [InlineData("201234567890")]
    [InlineData("20A23456789")]
    public void RechazaRucConFormatoInvalido(string ruc) => Assert.Contains("11 dígitos", SunatService.ValidateRucFormat(ruc));

    [Fact]
    public void RechazaRucDePersonaNatural() => Assert.Contains("comiencen con 20", SunatService.ValidateRucFormat("10123456789"));

    [Fact]
    public void AceptaFormatoDePersonaJuridica() => Assert.Null(SunatService.ValidateRucFormat("20123456789"));
}
