using System.Globalization;
using System.Text.RegularExpressions;
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
        // The sheet sizes the code; a width/height on the root <svg> would fight it.
        var svg = Qr.ToSvg(CatalogUrl);

        var root = svg[svg.IndexOf("<svg", StringComparison.Ordinal)..(svg.IndexOf('>', svg.IndexOf("<svg", StringComparison.Ordinal)) + 1)];
        Assert.Contains("viewBox", root, StringComparison.Ordinal);
        Assert.DoesNotContain("width=", root, StringComparison.Ordinal);
        Assert.DoesNotContain("height=", root, StringComparison.Ordinal);
    }

    [Fact]
    public void Pads_the_symbol_with_the_four_module_quiet_zone_the_spec_requires()
    {
        // Assert the padding, not the symbol size: which version a URL lands on is the encoder's call.
        var side = ViewBoxSide(Qr.ToSvg(CatalogUrl));
        var modules = side - (2 * 4);

        // Every QR version is an odd module count from 21 (v1) to 177 (v40).
        Assert.InRange(modules, 21, 177);
        Assert.Equal(1, modules % 2);
    }

    [Fact]
    public void A_longer_url_grows_the_symbol_but_not_the_quiet_zone()
    {
        var small = ViewBoxSide(Qr.ToSvg(CatalogUrl));
        var large = ViewBoxSide(Qr.ToSvg(CatalogUrl + new string('x', 300)));

        Assert.True(large > small, $"a 300-char-longer URL should need a bigger symbol ({large} vs {small})");
        Assert.Equal(1, (large - (2 * 4)) % 2);
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

    private static int ViewBoxSide(string svg)
    {
        var match = Regex.Match(svg, @"viewBox=""0 0 (\d+) (\d+)""");
        Assert.True(match.Success, "no square, module-unit viewBox on the SVG");
        Assert.Equal(match.Groups[1].Value, match.Groups[2].Value);
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
