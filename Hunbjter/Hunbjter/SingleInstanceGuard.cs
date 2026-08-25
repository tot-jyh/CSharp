using System.Runtime.InteropServices;

namespace Hunbjter;

/// <summary>
/// Stops a second copy of the app from ever starting. Without this, launching the app while one
/// is already running (including hidden in the tray) spins up a second WebView2 session and a
/// second monitor roster against the same favorites.json — duplicate live checks, duplicate
/// ffmpeg processes recording the same stream, and racing writes to the same file.
/// </summary>
internal static class SingleInstanceGuard
{
    // Fixed GUID, not a per-run one: every instance of the app must compute the same name to
    // find the same mutex and the same registered message.
    private const string InstanceId = "Hunbjter.Recorder.SingleInstance-3F1B2C1E-6E1B-4B2A-9C7B-2B2F1B0E7B1A";

    /// <summary>
    /// Test-only escape hatch (see Hunbjter.Tests.SingleInstanceGuardTests). Without this, running
    /// the tests while a real copy of the app is open always fails to acquire the mutex - which is
    /// the guard working correctly, but makes the tests unable to exercise their own acquire/release
    /// sequence in isolation. Production code must never set this.
    /// </summary>
    internal static string MutexNameOverride { private get; set; } = "";

    private static string MutexName => string.IsNullOrEmpty(MutexNameOverride) ? InstanceId : MutexNameOverride;

    private const uint SmtoAbortIfHung = 0x0002;
    private const uint WmNull = 0x0000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

    private static readonly IntPtr HwndBroadcast = new(0xffff);

    /// <summary>
    /// The message an already-running instance listens for on its main window. A plain broadcast
    /// reaches every top-level window in the session, including one that is hidden because the
    /// user minimized it to the tray, so it works regardless of the existing window's visibility.
    /// </summary>
    public static readonly int WakeMessage = RegisterWindowMessage(InstanceId + ".Wake");

    private static Mutex? mutex;

    /// <summary>
    /// Claims ownership for this process. Returns false when another instance already holds it —
    /// the caller must not proceed to create any windows or open any files in that case.
    /// </summary>
    public static bool TryAcquire()
    {
        mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);

        if (createdNew)
        {
            return true;
        }

        mutex.Dispose();
        mutex = null;
        return false;
    }

    /// <summary>
    /// Asks whatever instance owns the mutex to bring itself to the foreground. Uses
    /// SendMessageTimeout rather than a bare PostMessage so a hung existing instance cannot make
    /// this call block indefinitely.
    /// </summary>
    public static void WakeRunningInstance()
    {
        try
        {
            SendMessageTimeout(HwndBroadcast, WakeMessage, IntPtr.Zero, IntPtr.Zero, SmtoAbortIfHung, 2000, out _);
        }
        catch
        {
            // Best-effort: if this fails the new instance still exits, just without raising the
            // existing window. That is a strictly better outcome than running twice.
        }
    }

    /// <summary>Releases the mutex on normal exit so the next launch is recognized as the first.</summary>
    public static void Release()
    {
        try
        {
            mutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned (e.g. TryAcquire returned false on this process) — nothing to release.
        }
        finally
        {
            mutex?.Dispose();
            mutex = null;
        }
    }
}
