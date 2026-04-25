using XenoAtom.Terminal.UI.Styling;

namespace Ollama.TUI.Tests;

public class SettingsServiceTests
{
    [Test]
    public async Task ToTheme_Light_ReturnsDefaultLight()
    {
        var theme = SettingsService.ToTheme("Light");
        await Assert.That(theme).IsEqualTo(Theme.DefaultLight);
    }

    [Test]
    public async Task ToTheme_Terminal_ReturnsTerminal()
    {
        var theme = SettingsService.ToTheme("Terminal");
        await Assert.That(theme).IsEqualTo(Theme.Terminal);
    }

    [Test]
    public async Task ToTheme_Default_ReturnsDefault()
    {
        var theme = SettingsService.ToTheme("Default");
        await Assert.That(theme).IsEqualTo(Theme.Default);
    }

    [Test]
    public async Task ToTheme_UnknownString_FallsBackToDefault()
    {
        var theme = SettingsService.ToTheme("SomethingElse");
        await Assert.That(theme).IsEqualTo(Theme.Default);
    }

    [Test]
    public async Task Load_AlwaysReturnsNonNull()
    {
        var settings = SettingsService.Load();
        await Assert.That(settings).IsNotNull();
    }
}
