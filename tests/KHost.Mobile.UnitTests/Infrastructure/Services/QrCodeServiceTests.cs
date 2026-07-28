using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

public class QrCodeServiceTests
{
    private static readonly QrCodeService Qr = new();

    private const string CatalogUrl = "https://www.karafun.com/012345/";

    [Fact]
    public void Produces_an_svg_element()
    {
        var svg = Qr.ToSvg(CatalogUrl);

        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("</svg>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Scales_with_css_rather_than_a_fixed_pixel_size()
    {
        // The sheet sizes the code; a hardcoded width/height here would fight it.
        var svg = Qr.ToSvg(CatalogUrl);

        Assert.Contains("viewBox", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Encodes_the_quiet_zone_the_spec_requires()
    {
        // A 31-byte URL lands on version 3 (29 modules); 4 modules of quiet zone each side makes the viewBox 37.
        var svg = Qr.ToSvg(CatalogUrl);

        Assert.Contains("viewBox=\"0 0 37 37\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_venues_produce_different_codes()
    {
        var a = Qr.ToSvg("https://www.karafun.com/012345/");
        var b = Qr.ToSvg("https://www.karafun.com/543210/");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Same_input_is_stable()
    {
        Assert.Equal(Qr.ToSvg(CatalogUrl), Qr.ToSvg(CatalogUrl));
    }

    [Fact]
    public void Rejects_null_text()
    {
        Assert.Throws<ArgumentNullException>(() => Qr.ToSvg(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_text(string text)
    {
        Assert.Throws<ArgumentException>(() => Qr.ToSvg(text));
    }
}
