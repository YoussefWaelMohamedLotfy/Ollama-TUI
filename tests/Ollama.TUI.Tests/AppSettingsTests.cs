namespace Ollama.TUI.Tests;

public class AppSettingsTests
{
    [Test]
    public async Task DefaultOllamaServerUrl_IsLocalhost()
    {
        var settings = new AppSettings();
        await Assert.That(settings.OllamaServerUrl).IsEqualTo("http://localhost:11434");
    }

    [Test]
    public async Task DefaultTheme_IsDefault()
    {
        var settings = new AppSettings();
        await Assert.That(settings.Theme).IsEqualTo("Default");
    }

    [Test]
    public async Task DefaultSelectedModel_IsNull()
    {
        var settings = new AppSettings();
        await Assert.That(settings.SelectedModel).IsNull();
    }

    [Test]
    public async Task DefaultMcpToolsEnabled_IsEmptyDictionary()
    {
        var settings = new AppSettings();
        await Assert.That(settings.McpToolsEnabled).IsEmpty();
    }
}
