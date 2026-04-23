using System.Text;

using Ollama.TUI;

using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Threading;

var settings = SettingsService.Load();

using var session = Terminal.Open();

var ollama = new OllamaApiClient(new OllamaApiClient.Configuration
{
    Uri = new Uri(settings.OllamaServerUrl),
    JsonSerializerContext = AppJsonContext.Default
});

// ── App state ─────────────────────────────────────────────────────────────
State<bool> splashDone = new(false);
State<int> splashFrame = new(0);
State<bool> exit = new(false);
State<bool> modelsLoaded = new(false);
State<bool> modelSelected = new(false);
State<string?> selectedModel = new(null);
State<string> statusText = new("Connecting to Ollama…");
State<bool> isSending = new(false);
State<string?> currentResponse = new(null);
State<string?> currentThinking = new(null);
State<bool> currentModelSupportsThinking = new(false);
State<bool> settingsOpen = new(false);
State<bool> mcpToolsOpen = new(false);
State<Theme> currentTheme = new(SettingsService.ToTheme(settings.Theme));
State<string> serverUrlDisplay = new(settings.OllamaServerUrl);
var modelLoadStarted = false;

// ── Settings editing controls ─────────────────────────────────────────────
var themeChoices = new[] { "Default", "Light", "Terminal" };
var settingsUrlTextBox = new TextBox(settings.OllamaServerUrl).HorizontalAlignment(Align.Stretch);
var settingsThemeListBox = new ListBox<string>()
    .MinHeight(3)
    .MaxHeight(3)
    .HorizontalAlignment(Align.Stretch);
settingsThemeListBox.Items.Add("Default (Dark)");
settingsThemeListBox.Items.Add("Light");
settingsThemeListBox.Items.Add("Terminal");
settingsThemeListBox.SelectedIndex = Math.Max(0, Array.IndexOf(themeChoices, settings.Theme));

// ── MCP tool registry ─────────────────────────────────────────────────────
// Each entry describes one available tool. Enabled state is persisted in settings.
var toolRegistry = new (string Id, string Label, string Description, Func<OllamaSharp.Models.Chat.Tool> Create)[]
{
    ("get_current_datetime", "DateTime",        "Gets the current local date and time",              () => new DateTimeTool()),
    ("read_file",            "Read File",        "Reads the text contents of a file",                 () => new ReadFileTool()),
    ("write_file",           "Write File",       "Writes text content to a file (create/overwrite)",  () => new WriteFileTool()),
    ("list_directory",       "List Directory",   "Lists files and subdirectories in a directory",     () => new ListDirectoryTool()),
    ("create_directory",     "Create Directory", "Creates a new directory at the given path",         () => new CreateDirectoryTool()),
};

OllamaSharp.Models.Chat.Tool[] GetEnabledTools() =>
    toolRegistry
        .Where(t => settings.McpToolsEnabled.GetValueOrDefault(t.Id, true))
        .Select(t => t.Create())
        .ToArray();

// ── MCP tools screen ──────────────────────────────────────────────────────
var mcpToolsListBox = new ListBox<string>()
    .MinHeight(5)
    .MaxHeight(10)
    .HorizontalAlignment(Align.Stretch);

int mcpLabelWidth = toolRegistry.Max(t => t.Label.Length);

void RebuildMcpToolsList()
{
    mcpToolsListBox.Items.Clear();
    foreach (var (id, label, description, _) in toolRegistry)
    {
        var enabled = settings.McpToolsEnabled.GetValueOrDefault(id, true);
        mcpToolsListBox.Items.Add($"{(enabled ? "✓" : "✗")}  {label.PadRight(mcpLabelWidth)}  {description}");
    }
}
RebuildMcpToolsList();

void ToggleSelectedMcpTool()
{
    var idx = mcpToolsListBox.SelectedIndex;
    if (idx < 0 || idx >= toolRegistry.Length) return;
    var id = toolRegistry[idx].Id;
    var current = settings.McpToolsEnabled.GetValueOrDefault(id, true);
    settings.McpToolsEnabled[id] = !current;
    SettingsService.Save(settings);
    RebuildMcpToolsList();
    mcpToolsListBox.SelectedIndex = idx;
}

var mcpToggleButton = new Button("Toggle Enable/Disable").Tone(ControlTone.Primary)
    .IsEnabled(() => mcpToolsListBox.SelectedIndex >= 0);
var mcpCloseButton = new Button("Close");

mcpToggleButton.Click(() => ToggleSelectedMcpTool());

mcpCloseButton.Click(() => mcpToolsOpen.Value = false);

mcpToolsListBox.KeyDown((_, e) =>
{
    if (e.Key == TerminalKey.Enter || e.Key == TerminalKey.Space)
    {
        ToggleSelectedMcpTool();
        e.Handled = true;
    }
});

var mcpToolsScreen = new Center(
    new Group()
        .TopLeftText(" 🔧 MCP Tools")
        .Padding(2)
        .MaxWidth(80)
        .Content(
            new VStack(
                new TextBlock("Select a tool and press Toggle (or Enter/Space) to enable or disable it.") { Wrap = true },
                new TextBlock("Enabled tools (✓) are sent with every chat message to the model.") { Wrap = true },
                mcpToolsListBox,
                new HStack(mcpToggleButton, mcpCloseButton).Spacing(1)
            ).Spacing(1).HorizontalAlignment(Align.Stretch)
        )
        .HorizontalAlignment(Align.Stretch)
);

// ── Model list ─────────────────────────────────────────────────────────────
var modelItems = new List<string>();
var modelThinkingModes = new Dictionary<string, ThinkValue?>(StringComparer.OrdinalIgnoreCase);
var modelListBox = new ListBox<string>()
    .MinHeight(6)
    .MaxHeight(16)
    .HorizontalAlignment(Align.Stretch);

// ── Chat ───────────────────────────────────────────────────────────────────
Chat? chat = null;
CancellationTokenSource sendCts = new();
var toastHost = new ToastHost();
var log = new LogControl { MaxCapacity = 100_000 }.WrapText(true);
var promptEditor = new PromptEditor()
    .EscapeBehavior(PromptEditorEscapeBehavior.CancelCompletionOnly)
    .History(new PromptEditorHistory())
    .IsEnabled(() => modelSelected.Value && !isSending.Value)
    .AutoFocus(() => modelSelected.Value)
    .HorizontalAlignment(Align.Stretch);

// ── Actions ───────────────────────────────────────────────────────────────

// Escapes [ and ] so model-generated text is never mis-parsed as markup.
static string EscapeMarkup(string text) => text.Replace("[", "[[").Replace("]", "]]");

static bool HasCapability(ShowModelResponse? response, string capability) =>
    response?.Capabilities?.Any(value => string.Equals(value, capability, StringComparison.OrdinalIgnoreCase)) == true;

static bool HasFamily(Details? details, string family) =>
    string.Equals(details?.Family, family, StringComparison.OrdinalIgnoreCase) ||
    details?.Families?.Any(value => string.Equals(value, family, StringComparison.OrdinalIgnoreCase)) == true;

static ThinkValue? GetThinkingMode(ShowModelResponse? response)
{
    if (!HasCapability(response, "thinking"))
        return null;

    if (HasFamily(response?.Details, "gpt_oss"))
        return ThinkValue.Medium;

    ThinkValue enabledThinking = true;
    return enabledThinking;
}

ThinkValue? GetThinkingModeForModel(string modelName)
{
    if (modelThinkingModes.TryGetValue(modelName, out var cachedThinkingMode))
        return cachedThinkingMode;

    try
    {
        var response = ollama.ShowModelAsync(new ShowModelRequest { Model = modelName }).GetAwaiter().GetResult();
        var thinkingMode = GetThinkingMode(response);
        modelThinkingModes[modelName] = thinkingMode;
        return thinkingMode;
    }
    catch (Exception ex)
    {
        modelThinkingModes[modelName] = null;
        ToastService.Warning($"Could not inspect {modelName} capabilities, so thinking was disabled. {ex.Message}");
        return null;
    }
}

void ResetChatScreen(string titleMarkup)
{
    log.Clear();
    log.AppendMarkupLine(titleMarkup);
    log.AppendMarkupLine("[dim]Enter sends  •  Ctrl+J inserts newline  •  ↑↓ history  •  Ctrl+N new chat  •  Ctrl+W switch model  •  Ctrl+T MCP tools  •  Ctrl+Q quit[/]");
    log.AppendLine(string.Empty);
    currentThinking.Value = null;
    currentResponse.Value = null;
    promptEditor.Text = null;
    isSending.Value = false;
    statusText.Value = "Ready";
}

void SendMessage(string prompt)
{
    if (isSending.Value || chat is null || string.IsNullOrWhiteSpace(prompt)) return;

    log.AppendMarkupLine("[primary bold]You[/]");
    log.AppendLine(prompt);
    log.AppendLine(string.Empty);

    isSending.Value = true;
    statusText.Value = currentModelSupportsThinking.Value ? "Thinking…" : "Generating…";
    currentResponse.Value = string.Empty;

    var dispatcher = Dispatcher.Current;
    // Capture local references so the background task is unaffected by SwitchModel()
    // nulling the shared `chat` or replacing `sendCts`.
    var chatRef = chat;
    var localCts = sendCts;

    _ = Task.Run(async () =>
    {
        var sb = new StringBuilder();
        var thinkSb = new StringBuilder();
        var thinkingLogged = false;

        void OnThink(object? _, string token)
        {
            thinkSb.Append(token);
            var snapshot = thinkSb.ToString();
            _ = dispatcher.InvokeAsync(() => currentThinking.Value = snapshot);
        }

        void OnToolCall(object? _, OllamaSharp.Models.Chat.Message.ToolCall toolCall)
        {
            var name = EscapeMarkup(toolCall.Function?.Name ?? "unknown");
            _ = dispatcher.InvokeAsync(() =>
                log.AppendMarkupLine($"[magenta]⚙ Calling tool: {name}()[/]"));
        }

        void OnToolResult(object? _, OllamaSharp.Tools.ToolResult result)
        {
            var name = EscapeMarkup(result.Tool?.Function?.Name ?? "unknown");
            var value = result.Result?.ToString() ?? string.Empty;
            _ = dispatcher.InvokeAsync(() =>
            {
                log.AppendMarkupLine($"[magenta]⚙ Tool result ({name}):[/]");
                log.AppendLine(value);
            });
        }

        chatRef.OnThink += OnThink;
        chatRef.OnToolCall += OnToolCall;
        chatRef.OnToolResult += OnToolResult;

        try
        {
            var tools = GetEnabledTools();
            await foreach (var token in chatRef.SendAsync(prompt, tools, imagesAsBase64: null, format: null, cancellationToken: localCts.Token))
            {
                if (token is null) continue;

                if (!thinkingLogged && thinkSb.Length > 0)
                {
                    var thinkSnapshot = thinkSb.ToString();
                    await dispatcher.InvokeAsync(() =>
                    {
                        log.AppendMarkupLine("[dim]💭 Thinking…[/]");
                        log.AppendLine(thinkSnapshot); // plain text — never parsed as markup
                        log.AppendLine(string.Empty);
                        currentThinking.Value = null;
                    });
                    thinkingLogged = true;
                }

                sb.Append(token);
                var snapshot = sb.ToString();
                await dispatcher.InvokeAsync(() => currentResponse.Value = snapshot);
            }

            // Flush any remaining thinking content not yet shown
            if (!thinkingLogged && thinkSb.Length > 0)
            {
                var remainingThink = thinkSb.ToString();
                await dispatcher.InvokeAsync(() =>
                {
                    log.AppendMarkupLine("[dim]💭 Thinking…[/]");
                    log.AppendLine(remainingThink); // plain text — never parsed as markup
                    log.AppendLine(string.Empty);
                    currentThinking.Value = null;
                });
            }

            var finalText = sb.ToString();
            await dispatcher.InvokeAsync(() =>
            {
                log.AppendMarkupLine($"[success bold]Assistant[/] [dim]({EscapeMarkup(selectedModel.Value ?? string.Empty)})[/]");
                log.AppendLine(finalText);
                log.AppendLine(string.Empty);
                currentResponse.Value = null;
                isSending.Value = false;
                statusText.Value = "Ready";
            });
        }
        catch (OperationCanceledException)
        {
            // Model was switched or chat was reset — nothing to do, UI already cleaned up.
        }
        catch (Exception ex)
        {
            if (!localCts.IsCancellationRequested)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    log.AppendMarkupLine("[error]Error:[/]");
                    log.AppendLine(ex.Message); // plain text — never parsed as markup
                    currentThinking.Value = null;
                    currentResponse.Value = null;
                    isSending.Value = false;
                    statusText.Value = "Error — check Ollama connection";
                });
            }
        }
        finally
        {
            chatRef.OnThink -= OnThink;
            chatRef.OnToolCall -= OnToolCall;
            chatRef.OnToolResult -= OnToolResult;
            if (!localCts.IsCancellationRequested)
            {
                await dispatcher.InvokeAsync(() => currentThinking.Value = null);
            }
        }
    });
}

void StartChat(string modelName)
{
    sendCts = new CancellationTokenSource();
    ollama.SelectedModel = modelName;
    var thinkingMode = GetThinkingModeForModel(modelName);
    chat = new Chat(ollama) { Think = thinkingMode };
    currentModelSupportsThinking.Value = thinkingMode is not null;
    selectedModel.Value = modelName;
    modelSelected.Value = true;
    settings.SelectedModel = modelName;
    SettingsService.Save(settings);
    ResetChatScreen($"[dim]━━━ Chat started with [accent]{EscapeMarkup(modelName)}[/] ━━━[/]");
}

void NewChat()
{
    if (selectedModel.Value is not { } model) return;

    sendCts.Cancel();
    sendCts = new CancellationTokenSource();
    ollama.SelectedModel = model;
    var thinkingMode = GetThinkingModeForModel(model);
    chat = new Chat(ollama) { Think = thinkingMode };
    currentModelSupportsThinking.Value = thinkingMode is not null;
    ResetChatScreen($"[dim]━━━ New chat with [accent]{EscapeMarkup(model)}[/] ━━━[/]");
}

void SwitchModel()
{
    if (!modelSelected.Value) return;
    sendCts.Cancel();
    isSending.Value = false;
    currentResponse.Value = null;
    currentThinking.Value = null;
    currentModelSupportsThinking.Value = false;
    modelSelected.Value = false;
    selectedModel.Value = null;
    chat = null;
    statusText.Value = "Select a model";
}

void RefreshModels()
{
    sendCts.Cancel();
    modelsLoaded.Value = false;
    modelLoadStarted = false;
    modelItems.Clear();
    modelListBox.Items.Clear();
    modelSelected.Value = false;
    selectedModel.Value = null;
    chat = null;
    currentThinking.Value = null;
    currentResponse.Value = null;
    currentModelSupportsThinking.Value = false;
    isSending.Value = false;
    statusText.Value = "Connecting to Ollama…";
    settings.SelectedModel = null;
    SettingsService.Save(settings);
}

// ── Prompt editor events ───────────────────────────────────────────────────
promptEditor.Accepted((_, e) =>
{
    var text = e.Text?.Trim() ?? string.Empty;
    promptEditor.Text = null;
    SendMessage(text);
});

// ── Model selection screen ─────────────────────────────────────────────────
var selectButton = new Button("Start Chat")
    .Tone(ControlTone.Primary)
    .IsEnabled(() => modelsLoaded.Value && modelListBox.SelectedIndex >= 0);

selectButton.Click(() =>
{
    var idx = modelListBox.SelectedIndex;
    if (idx >= 0 && idx < modelItems.Count)
        StartChat(modelItems[idx]);
});

modelListBox.KeyDown((_, e) =>
{
    if (e.Key == TerminalKey.Enter)
    {
        var idx = modelListBox.SelectedIndex;
        if (idx >= 0 && idx < modelItems.Count)
            StartChat(modelItems[idx]);
        e.Handled = true;
    }
});

// Pre-create the loaded-state VStack so modelListBox/selectButton are never re-parented
// on subsequent re-evaluations (would throw "visual already has a parent").
var modelBodyLoaded = (Visual)new VStack(
    new TextBlock(() => $"{modelItems.Count} model(s) available — Enter or double-click to start") { Wrap = true },
    modelListBox,
    selectButton
).Spacing(1).HorizontalAlignment(Align.Stretch);

var modelScreenBody = new ComputedVisual(() =>
{
    if (!modelsLoaded.Value)
        return new HStack(new Spinner(), new TextBlock("Connecting to Ollama…") { Wrap = true }).Spacing(1);

    if (modelItems.Count == 0)
        return (Visual)new VStack(
            new Markup("[error bold]No local models found[/]") { Wrap = true },
            new TextBlock("Pull a model first:") { Wrap = true },
            new Markup("[accent]  ollama pull llama3.2[/]") { Wrap = true }
        ).Spacing(1);

    return modelBodyLoaded;
});

var modelScreen = new Center(
    new Group()
        .TopLeftText(" Ollama TUI")
        .TopRightText(() => (Visual)new Markup($"[dim]{serverUrlDisplay.Value}[/]"))
        .Padding(2)
        .MaxWidth(70)
        .Content(modelScreenBody)
        .HorizontalAlignment(Align.Stretch)
);

// ── Chat screen ────────────────────────────────────────────────────────────
var thinkingPanel = new ComputedVisual(() =>
{
    if (currentThinking.Value is not { Length: > 0 } thinking) return null;

    return (Visual)new Group()
        .TopLeftText("[dim] 💭 Thinking…[/]")
        .Padding(1)
        .Content(new TextBlock(() => currentThinking.Value ?? string.Empty).Wrap(true))
        .HorizontalAlignment(Align.Stretch);
});

var streamingPanel = new ComputedVisual(() =>
{
    if (!isSending.Value) return null;

    return (Visual)new Group()
        .TopLeftText(" Assistant (streaming…)")
        .Padding(1)
        .Content(new TextBlock(() => currentResponse.Value ?? string.Empty).Wrap(true))
        .HorizontalAlignment(Align.Stretch);
});

var sendButton = new Button("Send")
    .Tone(ControlTone.Primary)
    .IsEnabled(() => modelSelected.Value && !isSending.Value);

sendButton.Click(() =>
{
    var text = promptEditor.Text?.Trim();
    if (!string.IsNullOrEmpty(text))
    {
        promptEditor.Text = null;
        SendMessage(text);
    }
});

var inputArea = new Group()
    .TopLeftText("Message  [dim](Enter=send  Ctrl+J=newline  ↑↓=history)[/]")
    .Padding(1)
    .Content(new HStack(promptEditor, sendButton).Spacing(1).HorizontalAlignment(Align.Stretch))
    .HorizontalAlignment(Align.Stretch);

var chatScreen = new DockLayout()
    .Content(log.HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch))
    .Bottom(new VStack(thinkingPanel, streamingPanel, inputArea).Spacing(0).HorizontalAlignment(Align.Stretch))
    .HorizontalAlignment(Align.Stretch)
    .VerticalAlignment(Align.Stretch);

// ── Settings screen ────────────────────────────────────────────────────────
var saveButton = new Button("Save").Tone(ControlTone.Primary);
var cancelButton = new Button("Cancel");

saveButton.Click(() =>
{
    var url = settingsUrlTextBox.Text?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
    {
        ToastService.Error("Invalid URL. Please enter a valid Ollama server URL.");
        return;
    }

    var idx = Math.Clamp(settingsThemeListBox.SelectedIndex, 0, themeChoices.Length - 1);
    var selectedTheme = themeChoices[idx];

    var urlChanged = !string.Equals(url, settings.OllamaServerUrl, StringComparison.OrdinalIgnoreCase);
    settings.OllamaServerUrl = url;
    settings.Theme = selectedTheme;
    SettingsService.Save(settings);

    currentTheme.Value = SettingsService.ToTheme(selectedTheme);
    serverUrlDisplay.Value = url;

    if (urlChanged)
    {
        ollama = new OllamaApiClient(new OllamaApiClient.Configuration
        {
            Uri = new Uri(url),
            JsonSerializerContext = AppJsonContext.Default
        });
        RefreshModels();
    }

    settingsOpen.Value = false;
    ToastService.Success("Settings saved.");
});

cancelButton.Click(() => settingsOpen.Value = false);

var settingsScreen = new Center(
    new Group()
        .TopLeftText(" ⚙ Settings")
        .Padding(2)
        .MaxWidth(70)
        .Content(
            new VStack(
                new TextBlock("Ollama Server URL") { Wrap = true },
                settingsUrlTextBox,
                new TextBlock("Theme") { Wrap = true },
                settingsThemeListBox,
                new HStack(saveButton, cancelButton).Spacing(1)
            ).Spacing(1).HorizontalAlignment(Align.Stretch)
        )
        .HorizontalAlignment(Align.Stretch)
);

// ── Splash screen ─────────────────────────────────────────────────────────
// ~1.5 s at 16 ms per frame: 4 ASCII lines revealed one-by-one, then a
// loading bar that fills to 100 % before handing off to the main UI.
const int SplashDuration = 110;

string[] splashAscii =
[
    @"  ___   _  _                      _____  _   _  ___ ",
    @" / _ \ | || | __ _  _ __   __ _  |_   _|| | | ||_ _|",
    @"| (_) || || |/ _` || '  \ / _` |   | |  | |_| | | | ",
    @" \___/ |_||_|\__,_||_|_|_|\__,_|   |_|   \___/ |___|",
];

const int SplashAsciiLines = 4;
const int SplashBarDelayFrames = 5;   // pause after last ASCII line before bar appears
const int SplashColorTransitionFrame = 12; // frame at which art color brightens
const int SplashTaglineDelay = 3;     // frames after bar start before tagline appears
const int SplashBarStart = SplashAsciiLines * 8 + SplashBarDelayFrames; // frame when bar first appears
const int SplashBarWidth = 44;

var splashScreen = new Center(
    new VStack(
        new ComputedVisual(() =>
        {
            int frame = splashFrame.Value;
            // Reveal one ASCII art line per 8 frames; first line shows immediately.
            int visibleLines = Math.Min(SplashAsciiLines, frame / 8 + 1);
            string color = frame < SplashColorTransitionFrame ? "dim" : "accent bold";

            var artLines = new List<Visual>();
            for (int i = 0; i < SplashAsciiLines; i++)
            {
                artLines.Add(i < visibleLines
                    ? (Visual)new Markup($"[{color}]{splashAscii[i]}[/]") { Wrap = false }
                    : new TextBlock(string.Empty) { Wrap = false });
            }
            return (Visual)new VStack(artLines.ToArray()).Spacing(0);
        }),
        new TextBlock(string.Empty) { Wrap = false },
        new ComputedVisual(() =>
        {
            int frame = splashFrame.Value;
            if (frame < SplashBarStart) return null;

            int denom = Math.Max(1, SplashDuration - SplashBarStart - 1);
            int filled = Math.Min(SplashBarWidth, (frame - SplashBarStart) * SplashBarWidth / denom);
            string bar = new string('━', filled) + new string('─', SplashBarWidth - filled);
            return (Visual)new Markup($"  [dim]┤{bar}├[/]") { Wrap = false };
        }),
        new ComputedVisual(() =>
        {
            int frame = splashFrame.Value;
            if (frame < SplashBarStart + SplashTaglineDelay) return null;
            return (Visual)new Markup("  [dim]Your local AI companion[/]") { Wrap = false };
        })
    ).Spacing(0)
);

// ── Main layout ────────────────────────────────────────────────────────────
var header = new Header
{
    Left = new Markup("[bold] Ollama TUI[/]") { Wrap = true },
    Center = new TextBlock(() => selectedModel.Value is { } m ? $"Model: {m}" : "Select a model to get started") { Wrap = true },
    Right = new TextBlock(() => isSending.Value ? (currentModelSupportsThinking.Value ? "Thinking…" : "Generating…") : statusText.Value) { Wrap = true },
};

var footer = new Footer
{
    Left = new TextBlock(() => $"Status: {statusText.Value}") { Wrap = true },
    Right = new Markup("[dim]Ctrl+Q quit  •  Ctrl+N new chat  •  Ctrl+W switch model  •  Ctrl+P settings  •  Ctrl+T MCP tools[/]") { Wrap = true },
};

var mainLayout = new DockLayout()
    .Top(header)
    .Content(new ComputedVisual(() =>
        mcpToolsOpen.Value ? (Visual)mcpToolsScreen :
        settingsOpen.Value  ? (Visual)settingsScreen  :
        modelSelected.Value ? chatScreen              : modelScreen))
    .Bottom(new VStack(new CommandBar(), footer).Spacing(0))
    .HorizontalAlignment(Align.Stretch)
    .VerticalAlignment(Align.Stretch);

// Root switches between full-screen splash and the main layout.
var root = new ComputedVisual(() => splashDone.Value ? (Visual)mainLayout : splashScreen)
    .HorizontalAlignment(Align.Stretch)
    .VerticalAlignment(Align.Stretch);

toastHost.Content(root);
((Visual)toastHost).Style<Visual, Theme>(currentTheme);

// ── Commands ──────────────────────────────────────────────────────────────
toastHost.AddCommand(new Command
{
    Id = "App.Quit",
    LabelMarkup = "Quit",
    DescriptionMarkup = "Exit Ollama TUI",
    Gesture = new KeyGesture(TerminalChar.CtrlQ, TerminalModifiers.Ctrl),
    Importance = CommandImportance.Primary,
    Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
    Execute = _ => exit.Value = true,
});

toastHost.AddCommand(new Command
{
    Id = "App.NewChat",
    LabelMarkup = "New Chat",
    DescriptionMarkup = "Start a fresh conversation with the current model",
    Gesture = new KeyGesture(TerminalChar.CtrlN, TerminalModifiers.Ctrl),
    Importance = CommandImportance.Primary,
    Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
    Execute = _ => NewChat(),
});

toastHost.AddCommand(new Command
{
    Id = "App.SwitchModel",
    LabelMarkup = "Switch Model",
    DescriptionMarkup = "Go back to model selection",
    Gesture = new KeyGesture(TerminalChar.CtrlW, TerminalModifiers.Ctrl),
    Importance = CommandImportance.Primary,
    Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
    Execute = _ => SwitchModel(),
});

toastHost.AddCommand(new Command
{
    Id = "App.Settings",
    LabelMarkup = "Settings",
    DescriptionMarkup = "Open settings",
    Gesture = new KeyGesture(TerminalChar.CtrlP, TerminalModifiers.Ctrl),
    Importance = CommandImportance.Primary,
    Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
    Execute = _ =>
    {
        settingsUrlTextBox.Text = settings.OllamaServerUrl;
        settingsThemeListBox.SelectedIndex = Math.Max(0, Array.IndexOf(themeChoices, settings.Theme));
        settingsOpen.Value = true;
    },
});

toastHost.AddCommand(new Command
{
    Id = "App.McpTools",
    LabelMarkup = "MCP Tools",
    DescriptionMarkup = "Manage tools available to the AI model",
    Gesture = new KeyGesture(TerminalChar.CtrlT, TerminalModifiers.Ctrl),
    Importance = CommandImportance.Primary,
    Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
    Execute = _ =>
    {
        RebuildMcpToolsList();
        mcpToolsOpen.Value = true;
    },
});

// ── App loop ──────────────────────────────────────────────────────────────
Func<TerminalRunningContext, ValueTask<TerminalLoopResult>> loopFunc= async (ctx) =>
{
    if (exit.Value) return TerminalLoopResult.Stop;

    // Advance the splash animation; model loading starts only after it finishes.
    if (!splashDone.Value)
    {
        splashFrame.Value++;
        if (splashFrame.Value >= SplashDuration)
            splashDone.Value = true;
        await Task.Delay(16);
        return TerminalLoopResult.Continue;
    }

    if (!modelLoadStarted)
    {
        modelLoadStarted = true;
        try
        {
            var models = await ollama.ListLocalModelsAsync();
            foreach (var m in models ?? [])
            {
                var name = m.Name ?? string.Empty;
                if (!string.IsNullOrEmpty(name))
                {
                    modelItems.Add(name);
                    modelListBox.Items.Add(name);
                }
            }
            modelsLoaded.Value = true;
            if (modelItems.Count > 0)
            {
                var lastIndex = settings.SelectedModel is not null
                    ? modelItems.IndexOf(settings.SelectedModel)
                    : -1;
                var startIndex = lastIndex >= 0 ? lastIndex : 0;
                modelListBox.SelectedIndex = startIndex;
                StartChat(modelItems[startIndex]);
            }
            else
            {
                statusText.Value = "No models found";
            }
        }
        catch (Exception ex)
        {
            modelsLoaded.Value = true;
            statusText.Value = $"Cannot connect to Ollama: {ex.Message}";
        }
        return TerminalLoopResult.Continue;
    }

    await Task.Delay(16);
    return TerminalLoopResult.Continue;
};

await Terminal.RunAsync(toastHost, loopFunc, new TerminalRunOptions());
