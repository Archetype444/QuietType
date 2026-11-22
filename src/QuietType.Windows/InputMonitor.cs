using System.ComponentModel;
using System.Runtime.InteropServices;
namespace QuietType.Windows;

internal sealed class InputMonitor : IDisposable
{
    private readonly Native.HookProc keyboardProc, mouseProc;
    private readonly nint keyboard, mouse;
    private long revision, lastInput, boundaryRevision;
    public AutoResetEvent Activity { get; } = new(false);
    public long Revision => Interlocked.Read(ref revision);
    public long LastInput => Interlocked.Read(ref lastInput);
    public long BoundaryRevision => Interlocked.Read(ref boundaryRevision);
    public InputMonitor()
    {
        keyboardProc = OnKeyboard; mouseProc = OnMouse;
        var module = Native.GetModuleHandle(null);
        keyboard = Native.SetWindowsHookEx(13, keyboardProc, module, 0);
        mouse = Native.SetWindowsHookEx(14, mouseProc, module, 0);
        if (keyboard == 0 || mouse == 0) { Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }
    public void Invalidate()
    {
        Interlocked.Increment(ref revision);
        Interlocked.Exchange(ref lastInput, Environment.TickCount64);
        Interlocked.Exchange(ref boundaryRevision, 0);
        Activity.Set();
    }
    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<Native.KBD>(data);
            // Ignore only our own correction batches; other input invalidates pending work.
            if (key.Extra != Native.InputTag)
            {
                var current = Interlocked.Increment(ref revision);
                Interlocked.Exchange(ref lastInput, Environment.TickCount64);
                if (message is 0x100 or 0x104 && (Native.ModifierDown || key.Key is 0x08 or 0x09 or 0x0D or 0x11 or 0x12 or 0x1B or >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x5B or 0x5C))
                    Interlocked.Exchange(ref boundaryRevision, 0);
                if (message == 0x100 && !Native.ModifierDown && key.Key is 0x20 or 0xBA or 0xBC or 0xBE or 0xBF)
                    Interlocked.Exchange(ref boundaryRevision, current);
                Activity.Set();
            }
        }
        return Native.CallNextHookEx(keyboard, code, message, data);
    }
    private nint OnMouse(int code, nint message, nint data)
    {
        // Clicks and wheel input invalidate text/caret assumptions. Pointer motion alone doesn't.
        if (code >= 0 && message is 0x201 or 0x204 or 0x207 or 0x20A or 0x20E) Invalidate();
        return Native.CallNextHookEx(mouse, code, message, data);
    }
    public void Dispose() { if (keyboard != 0) Native.UnhookWindowsHookEx(keyboard); if (mouse != 0) Native.UnhookWindowsHookEx(mouse); Activity.Dispose(); }
}

internal sealed class Hotkeys : NativeWindow, IDisposable
{
    private readonly Action toggle, undo;
    public bool ToggleAvailable { get; }
    public bool UndoAvailable { get; }
    public Hotkeys(Action toggle, Action undo)
    {
        this.toggle = toggle; this.undo = undo;
        CreateHandle(new CreateParams { Caption = "QuietType.Hotkeys", Parent = new nint(-3) });
        ToggleAvailable = Native.RegisterHotKey(Handle, 1, 0x4003, (uint)'P');
        UndoAvailable = Native.RegisterHotKey(Handle, 2, 0x4003, (uint)'Z');
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x312) { if (message.WParam == 1) toggle(); else if (message.WParam == 2) undo(); }
        base.WndProc(ref message);
    }
    public void Dispose() { Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2); DestroyHandle(); }
}
