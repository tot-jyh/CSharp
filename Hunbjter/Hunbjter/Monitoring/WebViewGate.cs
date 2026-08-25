using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

/// <summary>Lower value wins. Manual actions should never queue behind a batch of automatic checks.</summary>
public enum GatePriority
{
    Manual = 0,
    Urgent = 1,
    Normal = 2
}

/// <summary>
/// Serializes access to the single shared WebView2.
///
/// This is a correctness requirement, not an optimization. A live check does
/// <c>Navigate(/play/{id})</c>, awaits a CDP <c>Network.responseReceived</c>, then runs
/// <c>Network.disable</c> in its finally. Two overlapping checks therefore (a) cancel each
/// other's page load and (b) tear down CDP events out from under each other, which surfaces as
/// "응답 모델 불일치" or a spurious "확인 실패".
///
/// <see cref="SemaphoreSlim"/> is not used because it is roughly FIFO and has no notion of
/// priority — the 10s paid-room retry would queue behind every 300s model, which is exactly the
/// case where per-model urgency is supposed to matter.
/// </summary>
public sealed class WebViewGate
{
    private readonly object sync = new();
    private readonly List<Waiter> waiters = [];
    private readonly Func<WebView2> webViewAccessor;

    private bool held;
    private long sequence;
    private int pendingManual;

    public WebViewGate(Func<WebView2> webViewAccessor)
    {
        this.webViewAccessor = webViewAccessor;
    }

    /// <summary>
    /// Lets a long automatic sweep yield at its next iteration boundary so a user-initiated
    /// check does not wait behind the whole batch.
    /// </summary>
    public bool HasPendingManual
    {
        get
        {
            lock (sync)
            {
                return pendingManual > 0;
            }
        }
    }

    public Task<WebViewLease> AcquireAsync(GatePriority priority, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<WebViewLease>(cancellationToken);
        }

        Waiter waiter;

        lock (sync)
        {
            if (!held)
            {
                held = true;
                return Task.FromResult(new WebViewLease(this, webViewAccessor()));
            }

            waiter = new Waiter(priority, sequence++);
            waiters.Add(waiter);

            if (priority == GatePriority.Manual)
            {
                pendingManual++;
            }
        }

        if (cancellationToken.CanBeCanceled)
        {
            waiter.Registration = cancellationToken.Register(static state =>
            {
                var pending = (Waiter)state!;
                pending.Owner?.Withdraw(pending);
            }, waiter);
            waiter.Owner = this;
        }

        return waiter.Completion.Task;
    }

    internal void Release()
    {
        Waiter? next;

        lock (sync)
        {
            next = TakeNextWaiter();

            if (next is null)
            {
                held = false;
                return;
            }
        }

        next.Registration.Dispose();

        // The lease is handed over directly: `held` intentionally stays true, so ownership
        // passes from one holder to the next without briefly opening the gate.
        if (!next.Completion.TrySetResult(new WebViewLease(this, webViewAccessor())))
        {
            // The waiter was cancelled between selection and hand-off; try the next one.
            Release();
        }
    }

    private Waiter? TakeNextWaiter()
    {
        if (waiters.Count == 0)
        {
            return null;
        }

        var bestIndex = 0;
        for (var i = 1; i < waiters.Count; i++)
        {
            if (waiters[i].Priority < waiters[bestIndex].Priority
                || (waiters[i].Priority == waiters[bestIndex].Priority
                    && waiters[i].Sequence < waiters[bestIndex].Sequence))
            {
                bestIndex = i;
            }
        }

        var selected = waiters[bestIndex];
        waiters.RemoveAt(bestIndex);

        if (selected.Priority == GatePriority.Manual)
        {
            pendingManual--;
        }

        return selected;
    }

    private void Withdraw(Waiter waiter)
    {
        lock (sync)
        {
            if (waiters.Remove(waiter) && waiter.Priority == GatePriority.Manual)
            {
                pendingManual--;
            }
        }

        waiter.Completion.TrySetCanceled();
    }

    private sealed class Waiter(GatePriority priority, long sequence)
    {
        public GatePriority Priority { get; } = priority;

        public long Sequence { get; } = sequence;

        public TaskCompletionSource<WebViewLease> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenRegistration Registration { get; set; }

        public WebViewGate? Owner { get; set; }
    }
}

/// <summary>
/// Proof that the holder owns the WebView2. Passing this as an explicit parameter is what stops
/// a nested call (StartRecordingAsync → CheckFavoriteLiveAsync → GetRecordingHttpContextAsync)
/// from trying to re-acquire a non-reentrant gate and deadlocking against itself.
/// </summary>
public sealed class WebViewLease : IDisposable
{
    private WebViewGate? gate;

    internal WebViewLease(WebViewGate gate, WebView2 webView)
    {
        this.gate = gate;
        WebView = webView;
    }

    public WebView2 WebView { get; }

    public void Dispose()
    {
        // Interlocked so a double Dispose cannot release the gate twice.
        var owner = Interlocked.Exchange(ref gate, null);
        owner?.Release();
    }
}
