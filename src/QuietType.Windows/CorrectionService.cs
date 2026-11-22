using System.Diagnostics;
using QuietType.Core;
namespace QuietType.Windows;

internal sealed class CorrectionService : IDisposable
{
    private Settings settings;
    private readonly InputMonitor input;
    private readonly CancellationTokenSource stop = new();
    private readonly Thread thread;
    private volatile bool undoRequested, restoreUndoWindow;
    private Settings? appliedSettings;
    private long lastHandled, lastInference, lastModelUse;
    private ContextModel? model;
    private bool modelFailed;
    private UndoRecord? undo;
    private readonly string assetDirectory;
    public event Action<string>? StatusChanged;
    public event Action? Corrected;
    public double LastInferenceMs { get; private set; }
    public string Status { get; private set; } = "Starting…";
    private sealed record UndoRecord(TextSnapshot Expected, string Original, string Replacement, string Tail, long Created);
    public CorrectionService(Settings settings, InputMonitor input, string assetDirectory)
    {
        this.settings = settings; this.input = input; this.assetDirectory = assetDirectory;
        thread = new Thread(Loop) { IsBackground = true, Name = "QuietType correction", Priority = ThreadPriority.BelowNormal };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }
    public void Configure(Settings next) { Volatile.Write(ref settings, next); input.Invalidate(); }
    public void Undo(bool restoreWindow) { restoreUndoWindow = restoreWindow; undoRequested = true; }
    private void SetStatus(string status) { if (Status == status) return; Status = status; StatusChanged?.Invoke(status); }
    private void Loop()
    {
        try
        {
            var lexicon = new Lexicon(Path.Combine(assetDirectory, "dictionary", "frequency.txt"));
            var engine = new CorrectionEngine(lexicon);
            SetStatus("Ready · offline");
            while (!stop.IsCancellationRequested)
            {
                stop.Token.WaitHandle.WaitOne(60);
                if (stop.IsCancellationRequested) break;
                var current = Volatile.Read(ref settings);
                if (!ReferenceEquals(current, appliedSettings))
                {
                    appliedSettings = current; modelFailed = false;
                    if (!current.Enabled || !current.UseAi) { model?.Dispose(); model = null; }
                    undo = null;
                    SetStatus(!current.Enabled ? "Paused" : current.UseAi ? "Ready · offline" : "Ready · spelling only");
                }
                if (undo is not null && Environment.TickCount64 - undo.Created > 20000) undo = null;
                if (undoRequested) { undoRequested = false; TryUndo(current); continue; }
                if (!current.Enabled) continue;
                if (model is not null && Environment.TickCount64 - lastModelUse > 180000) { model.Dispose(); model = null; SetStatus("Ready · AI sleeping"); }
                long boundary = input.BoundaryRevision;
                if (boundary == 0 || boundary == lastHandled || Environment.TickCount64 - input.LastInput < current.IdleDelayMs) continue;
                lastHandled = boundary;
                long revision = input.Revision;
                try
                {
                    var snapshot = TextAccess.Capture(current);
                    if (snapshot is null || revision != input.Revision) continue;
                    if (TextRules.FindTarget(snapshot.Before) is null) continue;
                    var correction = engine.Suggest(snapshot.Before, snapshot.After, current with { CorrectRealWords = false }, null);
                    if (correction is null && current.UseAi && !modelFailed && Environment.TickCount64 - lastInference >= 1200)
                    {
                        // Unknown/ambiguous words only. Already-correct prose doesn't wake the model.
                        if (engine.NeedsContext(snapshot.Before, current))
                        {
                            SetStatus("Checking locally…");
                            model ??= new ContextModel(Path.Combine(assetDirectory, "model"));
                            lastModelUse = Environment.TickCount64;
                            correction = engine.Suggest(snapshot.Before, snapshot.After, current, model);
                            LastInferenceMs = model.LastInferenceMs;
                            lastInference = Environment.TickCount64;
                            SetStatus("Ready · local AI");
                        }
                    }
                    if (correction is null || revision != input.Revision || !ReferenceEquals(current, Volatile.Read(ref settings))) continue;
                    var latest = TextAccess.Capture(current);
                    if (latest is null || revision != input.Revision || !TextAccess.SamePosition(snapshot, latest)) continue;
                    if (!TextRules.CanReplace(latest.Before, correction.Original, correction.Tail)) continue;
                    if (stop.IsCancellationRequested || Native.GetForegroundWindow() != snapshot.Window || revision != input.Revision) continue;
                    if (Native.ReplaceSuffix(correction.Original.Length + correction.Tail.Length, correction.Replacement + correction.Tail))
                    {
                        var expectedBefore = snapshot.Before[..correction.Start] + correction.Replacement + correction.Tail;
                        expectedBefore = TextRules.BoundedTail(expectedBefore, 384);
                        undo = new(snapshot with { Before = expectedBefore }, correction.Original, correction.Replacement, correction.Tail, Environment.TickCount64);
                        Corrected?.Invoke();
                    }
                    else { undo = null; SetStatus("Input unavailable · skipped"); }
                }
                catch (Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
                {
                    modelFailed = true; model?.Dispose(); model = null;
                    SetStatus("AI unavailable · spelling only");
                }
                catch (FileNotFoundException) { modelFailed = true; SetStatus("Model missing · spelling only"); }
                catch (Exception e) when (e is System.Windows.Automation.ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException)
                { /* A closed, protected, or unsupported field is deliberately skipped. No text is logged. */ }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { SetStatus("Assets unavailable · reinstall QuietType"); }
        catch (Exception) { SetStatus("Correction stopped · restart QuietType"); }
        finally { model?.Dispose(); model = null; undo = null; }
    }
    private void TryUndo(Settings current)
    {
        var item = undo; undo = null;
        if (item is null || Environment.TickCount64 - item.Created > 20000) { SetStatus("Nothing recent to undo"); return; }
        // Hotkey is raised while modifiers are held. Wait for release without blocking the tray.
        for (int i = 0; i < 40 && (Native.ModifierDown || Native.Down(0x10)); i++)
            if (stop.Token.WaitHandle.WaitOne(50)) return;
        if (restoreUndoWindow && !Native.SetForegroundWindow(item.Expected.Window)) return;
        try
        {
            var revision = input.Revision;
            var actual = TextAccess.Capture(current);
            if (actual is null || !TextAccess.SamePosition(item.Expected, actual) || input.Revision != revision) { SetStatus("Undo skipped · cursor or text changed"); return; }
            if (Native.GetForegroundWindow() != actual.Window) return;
            if (Native.ReplaceSuffix(item.Replacement.Length + item.Tail.Length, item.Original + item.Tail))
            {
                input.Invalidate();
                SetStatus("Undone · word protected this session");
                // Prevent a correction/undo loop without recording any surrounding text.
                var next = current with { PersonalWords = [..current.PersonalWords, item.Original.ToLowerInvariant()] };
                Volatile.Write(ref settings, next);
            }
        }
        catch (Exception e) when (e is System.Windows.Automation.ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or ArgumentException) { }
    }
    public void Dispose()
    {
        stop.Cancel();
        // An unresponsive accessibility provider must never hang Quit.
        if (thread.Join(500)) stop.Dispose();
    }
}
