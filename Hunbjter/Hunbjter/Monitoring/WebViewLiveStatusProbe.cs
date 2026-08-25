namespace Hunbjter;

/// <summary>
/// Asks pandalive through the shared WebView2. The caller must already hold the gate — the
/// lease is the proof, which is what keeps a nested call from deadlocking on re-acquisition.
/// </summary>
public sealed class WebViewLiveStatusProbe : ILiveStatusProbe
{
    private readonly PandaLiveService service;

    public WebViewLiveStatusProbe(PandaLiveService service)
    {
        this.service = service;
    }

    public event EventHandler<MonitorLogEventArgs>? LogRequested;

    public async Task<PandaLiveStatus> ProbeAsync(WebViewLease lease, FavoriteItem favorite, CancellationToken cancellationToken)
    {
        var status = await service.GetLiveStatusAsync(lease.WebView, favorite.PlatformUserId, cancellationToken);

        if (status.Success || !PandaMessages.IsSessionRelatedFailure(status.Message))
        {
            return status;
        }

        // A stale or unauthorized session is worth one re-prepare before reporting failure.
        Log($"{favorite.DisplayName}: 세션 상태 재확인 중");
        await PrepareSessionAsync(lease, "세션 재준비", cancellationToken);

        return await service.GetLiveStatusAsync(lease.WebView, favorite.PlatformUserId, cancellationToken);
    }

    public async Task PrepareSessionAsync(WebViewLease lease, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var session = await service.PrepareSessionAsync(lease.WebView, cancellationToken);
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
