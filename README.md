# Ollama TUI

A fast, keyboard-driven **Terminal UI** for chatting with your local [Ollama](https://ollama.com) models. Built with .NET 10 and compiled to a **NativeAOT** self-contained binary — no runtime required.

![NativeAOT Build Validation](https://github.com/YoussefWaelMohamedLotfy/Ollama-TUI/actions/workflows/nativeaot-validation.yml/badge.svg)

---

## Features

- **Model picker** — lists all locally available Ollama models on startup
- **Streaming responses** — see the assistant reply token-by-token in real time
- **Thinking/reasoning display** — shows the model's internal reasoning (`💭 Thinking…`) before the final answer for supported models
- **Tool calling** — built-in `get_current_datetime` tool; the model can call it automatically when asked about the time
- **Multi-line input** — press `Ctrl+J` to insert a newline in the prompt
- **Prompt history** — navigate previous messages with `↑` / `↓`
- **New chat** — reset the conversation without leaving the app (`Ctrl+N`)
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

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `Enter` | Send message |
| `Ctrl+J` | Insert newline in prompt |
| `↑` / `↓` | Navigate prompt history |
| `Ctrl+N` | Start a new chat (same model) |
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

---

## Tech Stack

| Library | Purpose |
|---|---|
| [OllamaSharp](https://github.com/awaescher/OllamaSharp) `5.4.25` | Ollama REST API client |
| [XenoAtom.Terminal.UI](https://github.com/xoofx/XenoAtom.Terminal) `2.8.1` | Terminal UI framework |
| .NET 10 + NativeAOT | Runtime & AOT compilation |