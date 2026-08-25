using Hunbjter;
using Xunit;

namespace Hunbjter.Tests;

/// <summary>
/// The gate hands out the single shared WebView2. These tests never touch a real browser —
/// the accessor returns null and no test dereferences <see cref="WebViewLease.WebView"/>.
/// </summary>
public sealed class WebViewGateTests
{
    private static WebViewGate CreateGate() => new(() => null!);

    [Fact]
    public void FirstAcquireCompletesImmediately()
    {
        var gate = CreateGate();

        var acquire = gate.AcquireAsync(GatePriority.Normal);

        Assert.True(acquire.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SecondAcquireWaitsUntilTheFirstLeaseIsReleased()
    {
        var gate = CreateGate();
        var first = await gate.AcquireAsync(GatePriority.Normal);

        var second = gate.AcquireAsync(GatePriority.Normal);
        Assert.False(second.IsCompleted);

        first.Dispose();

        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    /// <summary>
    /// The reason a plain SemaphoreSlim is not enough: a manual check must not queue behind a
    /// batch of automatic ones.
    /// </summary>
    [Fact]
    public async Task ManualWaiterIsServedBeforeEarlierNormalWaiters()
    {
        var gate = CreateGate();
        using var held = await gate.AcquireAsync(GatePriority.Normal);

        var order = new List<string>();

        var firstNormal = Track(gate.AcquireAsync(GatePriority.Normal), "normal-1");
        var secondNormal = Track(gate.AcquireAsync(GatePriority.Normal), "normal-2");
        var manual = Track(gate.AcquireAsync(GatePriority.Manual), "manual");

        Assert.True(gate.HasPendingManual);

        held.Dispose();
        await Task.WhenAll(firstNormal, secondNormal, manual);

        Assert.Equal("manual", order[0]);
        Assert.Equal(["manual", "normal-1", "normal-2"], order);
        Assert.False(gate.HasPendingManual);

        async Task Track(Task<WebViewLease> pending, string label)
        {
            using var lease = await pending;
            lock (order)
            {
                order.Add(label);
            }
        }
    }

    [Fact]
    public async Task UrgentOutranksNormalButYieldsToManual()
    {
        var gate = CreateGate();
        using var held = await gate.AcquireAsync(GatePriority.Normal);

        var order = new List<string>();
        var normal = Track(gate.AcquireAsync(GatePriority.Normal), "normal");
        var urgent = Track(gate.AcquireAsync(GatePriority.Urgent), "urgent");
        var manual = Track(gate.AcquireAsync(GatePriority.Manual), "manual");

        held.Dispose();
        await Task.WhenAll(normal, urgent, manual);

        Assert.Equal(["manual", "urgent", "normal"], order);

        async Task Track(Task<WebViewLease> pending, string label)
        {
            using var lease = await pending;
            lock (order)
            {
                order.Add(label);
            }
        }
    }

    [Fact]
    public async Task EqualPriorityKeepsArrivalOrder()
    {
        var gate = CreateGate();
        using var held = await gate.AcquireAsync(GatePriority.Normal);

        var order = new List<string>();
        var tasks = Enumerable.Range(0, 5)
            .Select(i => Track(gate.AcquireAsync(GatePriority.Normal), $"n{i}"))
            .ToArray();

        held.Dispose();
        await Task.WhenAll(tasks);

        Assert.Equal(["n0", "n1", "n2", "n3", "n4"], order);

        async Task Track(Task<WebViewLease> pending, string label)
        {
            using var lease = await pending;
            lock (order)
            {
                order.Add(label);
            }
        }
    }

    [Fact]
    public async Task CancellingAWaiterReleasesItsSlotAndDoesNotStallTheQueue()
    {
        var gate = CreateGate();
        var held = await gate.AcquireAsync(GatePriority.Normal);

        using var cancellation = new CancellationTokenSource();
        var cancelled = gate.AcquireAsync(GatePriority.Manual, cancellation.Token);
        var survivor = gate.AcquireAsync(GatePriority.Normal);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(gate.HasPendingManual);

        held.Dispose();

        var granted = await survivor.WaitAsync(TimeSpan.FromSeconds(5));
        granted.Dispose();
    }

    [Fact]
    public async Task DisposingALeaseTwiceReleasesTheGateOnlyOnce()
    {
        var gate = CreateGate();
        var lease = await gate.AcquireAsync(GatePriority.Normal);

        lease.Dispose();
        lease.Dispose();

        // A double release would have left the gate open, letting two holders in at once.
        var first = await gate.AcquireAsync(GatePriority.Normal);
        var second = gate.AcquireAsync(GatePriority.Normal);

        Assert.False(second.IsCompleted);

        first.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    [Fact]
    public async Task ContendedAccessNeverOverlaps()
    {
        var gate = CreateGate();
        var concurrent = 0;
        var maxObserved = 0;

        await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ =>
        {
            using var lease = await gate.AcquireAsync(GatePriority.Normal);
            var now = Interlocked.Increment(ref concurrent);
            InterlockedMax(ref maxObserved, now);
            await Task.Delay(2);
            Interlocked.Decrement(ref concurrent);
        }));

        Assert.Equal(1, maxObserved);

        static void InterlockedMax(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target)))
            {
                if (Interlocked.CompareExchange(ref target, value, current) == current)
                {
                    return;
                }
            }
        }
    }
}
