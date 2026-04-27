# Ollama TUI

A fast, keyboard-driven **Terminal UI** for chatting with your local [Ollama](https://ollama.com) models. Built with .NET 10 and compiled to a **NativeAOT** self-contained binary — no runtime required.

![NativeAOT Build Validation](https://github.com/YoussefWaelMohamedLotfy/Ollama-TUI/actions/workflows/nativeaot-validation.yml/badge.svg)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/YoussefWaelMohamedLotfy/Ollama-TUI/badge)](https://api.securityscorecards.dev/projects/github.com/YoussefWaelMohamedLotfy/Ollama-TUI)

---

## Features

- **Model picker** — lists all locally available Ollama models on startup
- **Streaming responses** — see the assistant reply token-by-token in real time
- **Thinking/reasoning display** — shows the model's internal reasoning (`💭 Thinking…`) before the final answer for supported models
- **Tool calling** — built-in file tools (`ReadFile`, `WriteFile`, `ListDirectory`, `CreateDirectory`) plus `get_current_datetime`; the model can call them automatically when needed
- **MCP tools** — connect external MCP servers and expose their tools to the model
- **Multi-line input** — press `Ctrl+J` to insert a newline in the prompt
- **Prompt history** — navigate previous messages with `↑` / `↓`
- **New chat** — reset the conversation without leaving the app (`Ctrl+N`)
- **Switch model** — return to model selection mid-session (`Ctrl+W`)
- **MCP tools panel** — manage MCP servers from inside the app (`Ctrl+T`)
- **Settings panel** — change the Ollama server URL and colour theme at runtime (`Ctrl+P`); settings persist to disk
- **Three themes** — Default (dark), Light, Terminal
- **NativeAOT** — single self-contained executable, instant startup, no .NET runtime installation needed
- **Cross-platform** — Windows, Linux, and macOS builds via CI

---

## Prerequisites

1. [Ollama](https://ollama.com/download) installed and running:
   ```bash
   ollama serve
   ```
2. At least one local model pulled:
   ```bash
   ollama pull gemma3
   ```

---

## Getting Started

### Download a release

Download the pre-built binary for your platform from the [Releases](https://github.com/YoussefWaelMohamedLotfy/Ollama-TUI/releases) page and run it directly — no installation required (Linux/macOS). On Windows, run the `Ollama-TUI-Setup.exe` installer.

### Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

**Run in development mode:**
```bash
dotnet run --project src/Ollama.TUI/Ollama.TUI.csproj
```

**Publish as a NativeAOT self-contained binary:**
```bash
dotnet publish src/Ollama.TUI/Ollama.TUI.csproj --configuration Release -o ./nativeaot-publish
```

> **Note (Windows):** NativeAOT requires the **Desktop development with C++** workload from Visual Studio Build Tools.

## Current solution contents

- **App project:** `src/Ollama.TUI/Ollama.TUI.csproj`
- **Tests:** `tests/Ollama.TUI.Tests/Ollama.TUI.Tests.csproj`
- **Target framework:** `.NET 10`
- **Publish mode:** `PublishAot=true`
- **Central package management:** enabled via `Directory.Packages.props`

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `Enter` | Send message |
| `Ctrl+J` | Insert newline in prompt |
| `↑` / `↓` | Navigate prompt history |
| `Ctrl+N` | Start a new chat (same model) |
| `Ctrl+W` | Switch to a different model |
| `Ctrl+T` | Open MCP tools panel |
| `Ctrl+P` | Open settings |
| `Ctrl+Q` | Quit |

---

## Settings

Settings are stored at:
- **Windows:** `%APPDATA%\ollama-tui\settings.json`
- **Linux/macOS:** `~/.config/ollama-tui/settings.json` *(follows `Environment.SpecialFolder.ApplicationData`)*

| Setting | Default | Description |
|---|---|---|
| `OllamaServerUrl` | `http://localhost:11434` | URL of the Ollama API server |
| `Theme` | `Default` | UI colour theme: `Default`, `Light`, or `Terminal` |
| `SelectedModel` | _none_ | Last selected Ollama model |
| `McpServers` | _empty_ | Configured external MCP servers and their enabled state |
| `McpToolsEnabled` | _empty_ | Persisted enable/disable state for MCP tools |

---

## MCP server support

The app can load external MCP servers in addition to the built-in file tools.

- **stdio transport** — run a local process and communicate over stdin/stdout
- **HTTP transport** — connect to an HTTP/SSE endpoint

Each MCP server can be enabled or disabled from the MCP tools panel, and the configuration is saved in settings.

## Tech Stack

| Library | Purpose |
|---|---|
| [OllamaSharp](https://github.com/awaescher/OllamaSharp) `5.4.25` | Ollama REST API client and tool calling |
| [ModelContextProtocol.Core](https://github.com/modelcontextprotocol) `1.2.0` | MCP client integration |
| [XenoAtom.Terminal.UI](https://github.com/xoofx/XenoAtom.Terminal) `2.9.4` | Terminal UI framework |
| [TUnit](https://github.com/askthecode/TUnit) `1.39.0` | Test framework |
| .NET 10 + NativeAOT | Runtime & AOT compilation |
