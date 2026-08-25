using Hunbjter;
using Xunit;

namespace Hunbjter.Tests;

/// <summary>
/// The mutex is process-wide by name, so a second acquire attempt behaves the same whether it
/// comes from this process or a second one — which is what lets this run as a normal unit test
/// instead of spawning a real second process.
/// </summary>
[Collection("SingleInstanceGuard")]
public sealed class SingleInstanceGuardTests
{
    // Isolated from the production mutex name: without this, running the suite while a real copy
    // of the app is open would always see TryAcquire() fail (correctly - that IS the feature) and
    // every test in this class would fail for a reason that has nothing to do with the code under
    // test. A fresh name per test process also means a crashed previous run can't wedge this one.
    static SingleInstanceGuardTests()
    {
        SingleInstanceGuard.MutexNameOverride = "Hunbjter.Tests.SingleInstanceGuard-" + Guid.NewGuid();
    }

    public SingleInstanceGuardTests()
    {
        // A previous test in this class failing mid-run could leave the mutex held; start clean.
        SingleInstanceGuard.Release();
    }

    [Fact]
    public void FirstAcquireSucceeds()
    {
        Assert.True(SingleInstanceGuard.TryAcquire());
        SingleInstanceGuard.Release();
    }

    [Fact]
    public void SecondAcquireFailsWhileTheFirstIsStillHeld()
    {
        Assert.True(SingleInstanceGuard.TryAcquire());

        Assert.False(SingleInstanceGuard.TryAcquire());

        SingleInstanceGuard.Release();
    }

    [Fact]
    public void AcquireSucceedsAgainAfterRelease()
    {
        Assert.True(SingleInstanceGuard.TryAcquire());
        SingleInstanceGuard.Release();

        Assert.True(SingleInstanceGuard.TryAcquire());
        SingleInstanceGuard.Release();
    }

    [Fact]
    public void ReleaseWithoutAnAcquireIsANoOp()
    {
        // Program.cs's finally block runs this even on the path where TryAcquire returned false.
        SingleInstanceGuard.Release();
        SingleInstanceGuard.Release();

        Assert.True(SingleInstanceGuard.TryAcquire());
        SingleInstanceGuard.Release();
    }

    [Fact]
    public void WakeMessageIsRegisteredAsARealSystemMessageId()
    {
        // RegisterWindowMessage returns a value in [0xC000, 0xFFFF); 0 means registration failed.
        Assert.InRange(SingleInstanceGuard.WakeMessage, 0xC000, 0xFFFF);
    }
}

/// <summary>
/// Forces every test that touches the process-wide mutex onto xunit's same test collection, so
/// they never run concurrently against each other.
/// </summary>
[CollectionDefinition("SingleInstanceGuard", DisableParallelization = true)]
public sealed class SingleInstanceGuardCollection;
