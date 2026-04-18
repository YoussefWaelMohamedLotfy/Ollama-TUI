using System.Text;

using Ollama.TUI;

using OllamaSharp;

using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Threading;

using var session = Terminal.Open();

var ollamaUri = new Uri("http://localhost:11434");
var ollama = new OllamaApiClient(new OllamaApiClient.Configuration
{
    Uri = ollamaUri,
    JsonSerializerContext = AppJsonContext.Default
});

// ── App state ─────────────────────────────────────────────────────────────
State<bool> exit = new(false);
State<bool> modelsLoaded = new(false);
State<bool> modelSelected = new(false);
State<string?> selectedModel = new(null);
State<string> statusText = new("Connecting to Ollama…");
State<bool> isSending = new(false);
State<string?> currentResponse = new(null);
State<string?> currentThinking = new(null);

// ── Model list ─────────────────────────────────────────────────────────────
var modelItems = new List<string>();
var modelListBox = new ListBox<string>()
    .MinHeight(6)
    .MaxHeight(16)
    .HorizontalAlignment(Align.Stretch);

// ── Chat ───────────────────────────────────────────────────────────────────
Chat? chat = null;
var toastHost = new ToastHost();
var log = new LogControl { MaxCapacity = 100_000 }.WrapText(true);
var promptEditor = new PromptEditor()
    .EscapeBehavior(PromptEditorEscapeBehavior.CancelCompletionOnly)
    .History(new PromptEditorHistory())
    .IsEnabled(() => modelSelected.Value && !isSending.Value)
    .AutoFocus(() => modelSelected.Value)
    .HorizontalAlignment(Align.Stretch);

// ── Actions ───────────────────────────────────────────────────────────────
void SendMessage(string prompt)
{
    if (isSending.Value || chat is null || string.IsNullOrWhiteSpace(prompt)) return;

    log.AppendMarkupLine("[primary bold]You[/]");
    log.AppendLine(prompt);
    log.AppendLine(string.Empty);

    isSending.Value = true;
    statusText.Value = "Thinking…";
    currentResponse.Value = string.Empty;

    var dispatcher = Dispatcher.Current;
    _ = Task.Run(async () =>
    {
        var sb = new StringBuilder();
        var thinkSb = new StringBuilder(); // shadow — readable on background thread
        var thinkingLogged = false;

        void OnThink(object? _, string token)
        {
            thinkSb.Append(token);
            var snapshot = thinkSb.ToString();
            _ = dispatcher.InvokeAsync(() => currentThinking.Value = snapshot);
        }

        void OnToolCall(object? _, OllamaSharp.Models.Chat.Message.ToolCall toolCall)
        {
            var name = toolCall.Function?.Name ?? "unknown";
            _ = dispatcher.InvokeAsync(() =>
                log.AppendMarkupLine($"[magenta]⚙ Calling tool: {name}()[/]"));
        }

        void OnToolResult(object? _, OllamaSharp.Tools.ToolResult result)
        {
            var name = result.Tool?.Function?.Name ?? "unknown";
            var value = result.Result?.ToString() ?? string.Empty;
            _ = dispatcher.InvokeAsync(() =>
                log.AppendMarkupLine($"[magenta]⚙ Tool result ({name}): {value}[/]"));
        }

        chat.OnThink += OnThink;
        chat.OnToolCall += OnToolCall;
        chat.OnToolResult += OnToolResult;

        try
        {
            var tools = new OllamaSharp.Models.Chat.Tool[] { new DateTimeTool() };
            await foreach (var token in chat.SendAsync(prompt, tools, imagesAsBase64: null, format: null, cancellationToken: default))
            {
                if (token is null) continue;

                if (!thinkingLogged && thinkSb.Length > 0)
                {
                    var thinkSnapshot = thinkSb.ToString();
                    await dispatcher.InvokeAsync(() =>
                    {
                        log.AppendMarkupLine("[dim]💭 Thinking…[/]");
                        log.AppendMarkupLine($"[dim]{thinkSnapshot}[/]");
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
                    log.AppendMarkupLine($"[dim]{remainingThink}[/]");
                    log.AppendLine(string.Empty);
                    currentThinking.Value = null;
                });
            }

            var finalText = sb.ToString();
            await dispatcher.InvokeAsync(() =>
            {
                log.AppendMarkupLine($"[success bold]Assistant[/] [dim]({selectedModel.Value})[/]");
                log.AppendLine(finalText);
                log.AppendLine(string.Empty);
                currentResponse.Value = null;
                isSending.Value = false;
                statusText.Value = "Ready";
            });
        }
        catch (Exception ex)
        {
            await dispatcher.InvokeAsync(() =>
            {
                log.AppendMarkupLine($"[error]Error: {ex.Message}[/]");
                currentThinking.Value = null;
                currentResponse.Value = null;
                isSending.Value = false;
                statusText.Value = "Error — check Ollama connection";
            });
        }
        finally
        {
            chat.OnThink -= OnThink;
            chat.OnToolCall -= OnToolCall;
            chat.OnToolResult -= OnToolResult;
            await dispatcher.InvokeAsync(() => currentThinking.Value = null);
        }
    });
}

void StartChat(string modelName)
{
    ollama.SelectedModel = modelName;
    chat = new Chat(ollama) { Think = true };
    selectedModel.Value = modelName;
    modelSelected.Value = true;
    log.AppendMarkupLine($"[dim]━━━ Chat started with [accent]{modelName}[/] ━━━[/]");
    log.AppendMarkupLine("[dim]Enter sends  •  Ctrl+J inserts newline  •  ↑↓ history  •  Ctrl+N new chat  •  Ctrl+Q quit[/]");
    log.AppendLine(string.Empty);
    statusText.Value = "Ready";
}

void NewChat()
{
    if (selectedModel.Value is not { } model) return;

    ollama.SelectedModel = model;
    chat = new Chat(ollama) { Think = true };
    log.AppendMarkupLine("[dim]━━━ New chat ━━━[/]");
    log.AppendLine(string.Empty);
    statusText.Value = "Ready";
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

    return (Visual)new VStack(
        new TextBlock(() => $"{modelItems.Count} model(s) available — Enter or double-click to start") { Wrap = true },
        modelListBox,
        selectButton
    ).Spacing(1).HorizontalAlignment(Align.Stretch);
});

var modelScreen = new Center(
    new Group()
        .TopLeftText(" Ollama TUI")
        .TopRightText($"[dim]{ollamaUri}[/]")
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

// ── Main layout ────────────────────────────────────────────────────────────
var header = new Header
{
    Left = new Markup("[bold] Ollama TUI[/]") { Wrap = true },
    Center = new TextBlock(() => selectedModel.Value is { } m ? $"Model: {m}" : "Select a model to get started") { Wrap = true },
    Right = new TextBlock(() => isSending.Value ? "Thinking…" : statusText.Value) { Wrap = true },
};

var footer = new Footer
{
    Left = new TextBlock(() => $"Status: {statusText.Value}") { Wrap = true },
    Right = new Markup("[dim]Ctrl+Q quit  •  Ctrl+N new chat[/]") { Wrap = true },
};

var root = new DockLayout()
    .Top(header)
    .Content(new ComputedVisual(() => modelSelected.Value ? (Visual)chatScreen : modelScreen))
    .Bottom(new VStack(new CommandBar(), footer).Spacing(0))
    .HorizontalAlignment(Align.Stretch)
    .VerticalAlignment(Align.Stretch);

toastHost.Content(root);

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

// ── App loop ──────────────────────────────────────────────────────────────
var modelLoadStarted = false;

Func<TerminalRunningContext, ValueTask<TerminalLoopResult>> loopFunc = async (ctx) =>
{
    if (exit.Value) return TerminalLoopResult.Stop;

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
            if (modelItems.Count > 0)
                modelListBox.SelectedIndex = 0;
            modelsLoaded.Value = true;
            statusText.Value = modelItems.Count > 0 ? "Select a model" : "No models found";
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
