namespace Ollama.TUI.Tests;

public class FileToolsTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void CreateTempDir()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"OllamaTUITests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void DeleteTempDir()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task ReadFile_ExistingFile_ReturnsContent()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(path, "hello world");

        var result = FileTools.ReadFile(path);

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task ReadFile_NonExistentFile_ReturnsErrorMessage()
    {
        var path = Path.Combine(_tempDir, "does_not_exist.txt");

        var result = FileTools.ReadFile(path);

        await Assert.That(result.StartsWith("Error reading file:")).IsTrue();
    }

    [Test]
    public async Task WriteFile_CreatesFileWithContent()
    {
        var path = Path.Combine(_tempDir, "output.txt");

        FileTools.WriteFile(path, "test content");

        var written = File.ReadAllText(path);
        await Assert.That(written).IsEqualTo("test content");
    }

    [Test]
    public async Task WriteFile_CreatesIntermediateDirectories()
    {
        var path = Path.Combine(_tempDir, "nested", "deep", "file.txt");

        FileTools.WriteFile(path, "data");

        await Assert.That(File.Exists(path)).IsTrue();
    }

    [Test]
    public async Task WriteFile_ReturnsSuccessMessage()
    {
        var path = Path.Combine(_tempDir, "out.txt");

        var result = FileTools.WriteFile(path, "abc");

        await Assert.That(result.Contains("3 characters")).IsTrue();
    }

    [Test]
    public async Task ListDirectory_ReturnsFileAndDirEntries()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));

        var result = FileTools.ListDirectory(_tempDir);

        await Assert.That(result.Contains("[file] a.txt")).IsTrue();
        await Assert.That(result.Contains("[dir]  subdir/")).IsTrue();
    }

    [Test]
    public async Task ListDirectory_NonExistentDirectory_ReturnsErrorMessage()
    {
        var path = Path.Combine(_tempDir, "missing");

        var result = FileTools.ListDirectory(path);

        await Assert.That(result.StartsWith("Error: directory")).IsTrue();
    }

    [Test]
    public async Task CreateDirectory_CreatesDirectory()
    {
        var path = Path.Combine(_tempDir, "newdir");

        FileTools.CreateDirectory(path);

        await Assert.That(Directory.Exists(path)).IsTrue();
    }

    [Test]
    public async Task CreateDirectory_ReturnsSuccessMessage()
    {
        var path = Path.Combine(_tempDir, "another");

        var result = FileTools.CreateDirectory(path);

        await Assert.That(result.Contains("created successfully")).IsTrue();
    }
}
