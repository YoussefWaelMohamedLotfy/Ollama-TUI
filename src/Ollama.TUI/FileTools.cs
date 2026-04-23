using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;

namespace Ollama.TUI;

internal sealed class ReadFileTool : Tool, IInvokableTool
{
    public ReadFileTool()
    {
        Function = new Function
        {
            Name = "read_file",
            Description = "Reads and returns the full text content of a file at the specified path. Use this to view source code or any text file.",
            Parameters = new Parameters
            {
                Properties = new Dictionary<string, Property>
                {
                    ["path"] = new Property
                    {
                        Type = "string",
                        Description = "The absolute or relative path to the file to read."
                    }
                },
                Required = ["path"]
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        if (args?.TryGetValue("path", out var pathObj) != true || pathObj?.ToString() is not { } path)
            return "Error: 'path' argument is required.";
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }
}

internal sealed class WriteFileTool : Tool, IInvokableTool
{
    public WriteFileTool()
    {
        Function = new Function
        {
            Name = "write_file",
            Description = "Writes text content to a file at the specified path, creating or overwriting it. Use this to save or update source code files.",
            Parameters = new Parameters
            {
                Properties = new Dictionary<string, Property>
                {
                    ["path"] = new Property
                    {
                        Type = "string",
                        Description = "The absolute or relative path to the file to write."
                    },
                    ["content"] = new Property
                    {
                        Type = "string",
                        Description = "The text content to write to the file."
                    }
                },
                Required = ["path", "content"]
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        if (args?.TryGetValue("path", out var pathObj) != true || pathObj?.ToString() is not { } path)
            return "Error: 'path' argument is required.";
        if (args?.TryGetValue("content", out var contentObj) != true || contentObj?.ToString() is not { } content)
            return "Error: 'content' argument is required.";
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
}

internal sealed class ListDirectoryTool : Tool, IInvokableTool
{
    public ListDirectoryTool()
    {
        Function = new Function
        {
            Name = "list_directory",
            Description = "Lists the files and subdirectories in the specified directory path. Useful for exploring project structure.",
            Parameters = new Parameters
            {
                Properties = new Dictionary<string, Property>
                {
                    ["path"] = new Property
                    {
                        Type = "string",
                        Description = "The absolute or relative path to the directory to list."
                    }
                },
                Required = ["path"]
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        if (args?.TryGetValue("path", out var pathObj) != true || pathObj?.ToString() is not { } path)
            return "Error: 'path' argument is required.";
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
}

internal sealed class CreateDirectoryTool : Tool, IInvokableTool
{
    public CreateDirectoryTool()
    {
        Function = new Function
        {
            Name = "create_directory",
            Description = "Creates a new directory (and any necessary parent directories) at the specified path.",
            Parameters = new Parameters
            {
                Properties = new Dictionary<string, Property>
                {
                    ["path"] = new Property
                    {
                        Type = "string",
                        Description = "The absolute or relative path of the directory to create."
                    }
                },
                Required = ["path"]
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        if (args?.TryGetValue("path", out var pathObj) != true || pathObj?.ToString() is not { } path)
            return "Error: 'path' argument is required.";
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
