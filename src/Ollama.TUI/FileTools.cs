using OllamaSharp;

namespace Ollama.TUI;

internal static class FileTools
{
    /// <summary>Reads and returns the full text content of a file. Use this to view source code or any text file.</summary>
    /// <param name="path">The absolute or relative path to the file to read.</param>
    [OllamaTool]
    public static string ReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    /// <summary>Writes text content to a file, creating or overwriting it. Use this to save or update source code files.</summary>
    /// <param name="path">The absolute or relative path to the file to write.</param>
    /// <param name="content">The text content to write to the file.</param>
    [OllamaTool]
    public static string WriteFile(string path, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
            return $"Successfully wrote {content.Length} characters to '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error writing file: {ex.Message}";
        }
    }

    /// <summary>Lists the files and subdirectories in a directory. Useful for exploring project structure.</summary>
    /// <param name="path">The absolute or relative path to the directory to list.</param>
    [OllamaTool]
    public static string ListDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return $"Error: directory '{path}' does not exist.";

            var entries = Directory.GetFileSystemEntries(path)
                .OrderBy(e => e)
                .Select(e => Directory.Exists(e) ? $"[dir]  {Path.GetFileName(e)}/" : $"[file] {Path.GetFileName(e)}");

            return string.Join("\n", entries);
        }
        catch (Exception ex)
        {
            return $"Error listing directory: {ex.Message}";
        }
    }

    /// <summary>Creates a new directory (and any necessary parent directories) at the given path.</summary>
    /// <param name="path">The absolute or relative path of the directory to create.</param>
    [OllamaTool]
    public static string CreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return $"Directory '{path}' created successfully.";
        }
        catch (Exception ex)
        {
            return $"Error creating directory: {ex.Message}";
        }
    }
}

