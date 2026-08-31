namespace Hunbjter;

/// <summary>
/// Asks the model's site through the shared WebView2. The caller must already hold the gate —
/// the lease is the proof, which is what keeps a nested call from deadlocking on re-acquisition.
/// Dispatches per <see cref="FavoriteItem.Platform"/> to the matching site service; both return
/// the same <see cref="PandaLiveStatus"/> shape so the rest of the monitor stays site-agnostic.
/// </summary>
public sealed class WebViewLiveStatusProbe : ILiveStatusProbe
{
    private readonly PandaLiveService pandaService;
    private readonly StripchatService stripchatService;

    public WebViewLiveStatusProbe(PandaLiveService pandaService, StripchatService stripchatService)
    {
        this.pandaService = pandaService;
        this.stripchatService = stripchatService;
    }

    public event EventHandler<MonitorLogEventArgs>? LogRequested;

    public async Task<PandaLiveStatus> ProbeAsync(WebViewLease lease, FavoriteItem favorite, CancellationToken cancellationToken)
    {
        if (PandaMessages.IsStripchatPlatform(favorite.Platform, favorite.ProfileUrl))
        {
            var stripStatus = await stripchatService.GetLiveStatusAsync(lease.WebView, favorite.PlatformUserId, cancellationToken);
            if (stripStatus.Success || !PandaMessages.IsSessionRelatedFailure(stripStatus.Message))
            {
                return stripStatus;
            }

            Log($"{favorite.DisplayName}: 세션 상태 재확인 중");
            await PrepareOneAsync(stripchatService.PrepareSessionAsync, lease, "세션 재준비 (스챗)", cancellationToken);
            return await stripchatService.GetLiveStatusAsync(lease.WebView, favorite.PlatformUserId, cancellationToken);
        }

        var status = await pandaService.GetLiveStatusAsync(lease.WebView, favorite.PlatformUserId, cancellationToken);

        if (status.Success || !PandaMessages.IsSessionRelatedFailure(status.Message))
        {
            return status;
        }

        // A stale or unauthorized session is worth one re-prepare before reporting failure.
        Log($"{favorite.DisplayName}: 세션 상태 재확인 중");
        await PrepareOneAsync(pandaService.PrepareSessionAsync, lease, "세션 재준비 (팬더)", cancellationToken);

        return await pandaService.GetLiveStatusAsync(lease.WebView, favorite.PlatformUserId, cancellationToken);
    }

    /// <summary>
    /// Refreshes both sites' sessions unconditionally. A manual check sweep or a recording start
    /// can involve either site (or the caller has no single favorite to key off at all - see
    /// MonitorRoster.RunManualAsync), so preparing just one would silently skip the other. Each
    /// site is independently best-effort: a site the user has never configured just logs a
    /// harmless failure here instead of blocking the other site's prep.
    /// </summary>
    public async Task PrepareSessionAsync(WebViewLease lease, string reason, CancellationToken cancellationToken)
    {
        await PrepareOneAsync(pandaService.PrepareSessionAsync, lease, $"{reason} (팬더)", cancellationToken);
        await PrepareOneAsync(stripchatService.PrepareSessionAsync, lease, $"{reason} (스챗)", cancellationToken);
    }

    private async Task PrepareOneAsync(
        Func<Microsoft.Web.WebView2.WinForms.WebView2, CancellationToken, Task<PandaSessionStatus>> prepare,
        WebViewLease lease,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await prepare(lease.WebView, cancellationToken);
            Log($"{reason}: 쿠키 {session.CookieCount}개, 사용자 정보 {(session.HasViewerUserIndex ? "확인" : "미확인")}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Session prep is best-effort: the check itself still gets a chance to succeed.
            Log($"{reason} 실패: {ex.Message}");
        }
    }

    private void Log(string message) => LogRequested?.Invoke(this, new MonitorLogEventArgs(message));
}
