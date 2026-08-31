using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

/// <summary>
/// Asks stripchat.com whether one model is live. Unlike pandalive, stripchat has no per-username
/// lookup API - live status comes from a curated homepage feed (<see cref="ModelsFeedUri"/>) that
/// the logged-in account happens to be shown, so an entry not found there is treated as "not live
/// right now" rather than an error (see <see cref="GetLiveStatusAsync"/>). Tracking an unpopular
/// model reliably requires it to be favorited on stripchat.com itself, since only the account's
/// own favoritesLimit-capped block is guaranteed to include it regardless of ranking.
/// </summary>
public sealed class StripchatService
{
    private static readonly Uri StripchatHomeUri = new("https://stripchat.com/");
    private static readonly Uri InitialDynamicUri = new("https://stripchat.com/api/front/v3/config/initial-dynamic?requestPath=%2F");
    private static readonly Uri ModelsFeedUri = new(
        "https://stripchat.com/api/front/v2/models?primaryTag=girls&limit=24&topLimit=61&favoritesLimit=24"
        + "&msBlock=true&byw=false&flags=0&srwm=false&rcmGrp=A&rbCnGr=true&iem=true&decMb=true&dmv=false"
        + "&ctryTop=true&mlfv=false&rectf=false&eab=false&sac=false&nic=true&shFv=true");

    public async Task<RecordingHttpContext> GetRecordingHttpContextAsync(WebView2 webView, CancellationToken cancellationToken = default)
    {
        await WebViewProfile.EnsureCoreAsync(webView);
        var userAgent = await GetUserAgentAsync(webView);
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = "Mozilla/5.0";
        }

        // Confirmed by direct capture: the CDN segment/media-playlist requests need no cookies
        // or Authorization at all, only a standard Referer/Origin/User-Agent - simpler than
        // pandalive, whose ffmpeg-facing requests still need the session cookie.
        var headerText = string.Join("\r\n", new[]
        {
            "Referer: https://stripchat.com/",
            "Origin: https://stripchat.com"
        }) + "\r\n";

        return new RecordingHttpContext(userAgent, headerText, 0);
    }

    public async Task<PandaSessionStatus> GetSessionStatusAsync(WebView2 webView, CancellationToken cancellationToken = default)
    {
        await WebViewProfile.EnsureCoreAsync(webView);

        try
        {
            await EnsureStripchatOriginAsync(webView, cancellationToken);
        }
        catch
        {
            // A partially loaded page can still expose cookies; keep checking below.
        }

        var cookieHeader = await BuildCookieHeaderAsync(webView);
        string? jwtToken = null;
        try
        {
            jwtToken = await TryGetJwtTokenAsync(webView, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Session-prep is best-effort here too; GetLiveStatusAsync is where a failure needs
            // to be seen, not this status snapshot.
        }

        return new PandaSessionStatus(
            !string.IsNullOrWhiteSpace(cookieHeader),
            CountCookiePairs(cookieHeader),
            !string.IsNullOrWhiteSpace(jwtToken));
    }

    public async Task<PandaSessionStatus> PrepareSessionAsync(WebView2 webView, CancellationToken cancellationToken = default)
    {
        await WebViewProfile.EnsureCoreAsync(webView);
        await NavigateAndWaitAsync(webView, StripchatHomeUri.ToString(), TimeSpan.FromSeconds(20), cancellationToken);
        await Task.Delay(800, cancellationToken);
        return await GetSessionStatusAsync(webView, cancellationToken);
    }

    public async Task<PandaLiveStatus> GetLiveStatusAsync(WebView2 webView, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new PandaLiveStatus { Success = false, Message = "아이디가 비어 있습니다." };
        }

        username = username.Trim();
        await WebViewProfile.EnsureCoreAsync(webView);

        try
        {
            await EnsureStripchatOriginAsync(webView, cancellationToken);

            string? jwtToken;
            try
            {
                jwtToken = await TryGetJwtTokenAsync(webView, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Matches PandaMessages.IsSessionRelatedFailure ("로그인" substring) so the probe
                // retries this once after re-preparing the session instead of treating it as a
                // hard failure straight away. The reason is surfaced here (rather than a generic
                // "토큰 없음" line) because there is no way to browse stripchat.com directly to
                // debug this further - the log has to carry the real cause.
                return new PandaLiveStatus { Success = false, Message = $"스챗 로그인이 필요합니다 (토큰 조회 실패: {ex.Message})" };
            }

            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                return new PandaLiveStatus { Success = false, Message = "스챗 로그인이 필요합니다 (세션 토큰 없음)" };
            }

            var body = await RequestModelsFeedAsync(webView, jwtToken, cancellationToken);
            return ParseModelsFeedResponse(body, username);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new PandaLiveStatus { Success = false, Message = $"스챗 확인 실패: {ex.Message}" };
        }
    }

    // Both calls below run *inside* the page (fetch() triggered by an injected script, result
    // returned via window.chrome.webview.postMessage), not through a bare HttpClient and not by
    // relying on ExecuteScriptAsync awaiting a returned Promise. Two things were tried and ruled
    // out before landing here:
    //  - A bare HttpClient with a cookie snapshot produced 401 "invalid token" from the models
    //    feed: initial-dynamic can rotate a session cookie on its response, and a real browser's
    //    own fetch() absorbs that Set-Cookie automatically before the very next request, but a
    //    HttpClient snapshotting cookies beforehand never sees it.
    //  - An async-IIFE script returned directly from ExecuteScriptAsync (relying on it awaiting
    //    the Promise) came back as "{}" instead of the fetch result - confirmed live via the "JSON
    //    value could not be converted to System.String" parse error this used to surface. So the
    //    Promise wasn't actually being awaited here.
    //  - Fetching the absolute "https://stripchat.com/..." URL threw "TypeError: Failed to fetch"
    //    - confirmed live via the app's own WebView2 DevTools to be a CORS rejection, because the
    //    WebView2 profile actually lands on a localized subdomain (ko.stripchat.com), not bare
    //    stripchat.com, and the API has no Access-Control-Allow-Origin for that cross-subdomain
    //    combination. Every fetch below therefore uses a *path-only* URL (PathAndQuery, no scheme/
    //    host) so it resolves same-origin against whatever subdomain the page actually loaded -
    //    exactly what the site's own scripts do (and why the real page never hits this at all).
    // The postMessage round trip below is the same pattern PandaLiveService.RequestLivePlayThroughBrowserAsync
    // already relies on, so it is proven to work against ExecuteScriptAsync in this app.
    private static async Task<string> TryGetJwtTokenAsync(WebView2 webView, CancellationToken cancellationToken)
    {
        var body = await FetchInPageAsync(webView, InitialDynamicUri.PathAndQuery, authorization: null, cancellationToken);

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("initialDynamic", out var initialDynamic)
            || initialDynamic.ValueKind != JsonValueKind.Object
            || !initialDynamic.TryGetProperty("jwtToken", out var jwtElement)
            || jwtElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"initial-dynamic 응답에 jwtToken이 없습니다: {TrimForLog(body)}");
        }

        return jwtElement.GetString() ?? throw new InvalidOperationException("jwtToken이 비어 있습니다.");
    }

    private static string TrimForLog(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= 300 ? value : value[..300] + "...";
    }

    private static Task<string> RequestModelsFeedAsync(WebView2 webView, string jwtToken, CancellationToken cancellationToken)
    {
        // "uniq" is just a cache-buster in the captured request; any value works.
        var uniq = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var url = $"{ModelsFeedUri.PathAndQuery}&uniq={uniq}";
        return FetchInPageAsync(webView, url, authorization: jwtToken, cancellationToken);
    }

    private static async Task<string> FetchInPageAsync(WebView2 webView, string url, string? authorization, CancellationToken cancellationToken)
    {
        var messageType = "stripchat-fetch-" + Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void WebMessageHandler(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var document = JsonDocument.Parse(e.WebMessageAsJson);
                var root = document.RootElement;
                if (GetString(root, "type") != messageType)
                {
                    return;
                }

                completion.TrySetResult(GetString(root, "body"));
            }
            catch
            {
                // Ignore unrelated/malformed messages from the embedded page.
            }
        }

        webView.CoreWebView2.WebMessageReceived += WebMessageHandler;
        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(BuildFetchScript(url, authorization, messageType));
            var text = await completion.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

            if (TryExtractFetchError(text, out var fetchError))
            {
                throw new InvalidOperationException(fetchError);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("응답 본문이 비어 있습니다.");
            }

            return text;
        }
        finally
        {
            webView.CoreWebView2.WebMessageReceived -= WebMessageHandler;
        }
    }

    private static string BuildFetchScript(string url, string? authorization, string messageType)
    {
        var urlJson = JsonSerializer.Serialize(url);
        var messageTypeJson = JsonSerializer.Serialize(messageType);
        var authorizationHeaderLine = authorization is null
            ? ""
            // No "Bearer " prefix - confirmed by capturing a real successful browser request's
            // Network headers, which send the raw JWT string as-is here, unlike the usual
            // "Authorization: Bearer <token>" convention. The prefix alone was enough to make the
            // server respond "invalid token", since the header no longer parses as a bare JWT.
            : $", 'Authorization': {JsonSerializer.Serialize(authorization)}";

        // Deliberately not an async IIFE returned to ExecuteScriptAsync - see the comment above
        // TryGetJwtTokenAsync for why. This is a plain synchronous IIFE that kicks the fetch off
        // and reports its result back via postMessage once it settles.
        return $$"""
            (function() {
                const report = (body) => window.chrome.webview.postMessage({ type: {{messageTypeJson}}, body });
                fetch({{urlJson}}, {
                    method: 'GET',
                    credentials: 'include',
                    headers: { 'Accept': 'application/json, text/plain, */*'{{authorizationHeaderLine}} }
                }).then((res) => res.text().then((text) => {
                    report(res.ok ? text : JSON.stringify({ __fetchError: `HTTP ${res.status}: ${text}` }));
                })).catch((error) => {
                    report(JSON.stringify({ __fetchError: String(error) }));
                });
            })();
            """;
    }

    private static bool TryExtractFetchError(string text, out string message)
    {
        message = "";
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("__fetchError", out var errorElement)
                && errorElement.ValueKind == JsonValueKind.String)
            {
                message = errorElement.GetString() ?? "fetch 실패";
                return true;
            }
        }
        catch (JsonException)
        {
            // Not JSON at all - not our sentinel, let the caller's own parsing surface the problem.
        }

        return false;
    }

    private static PandaLiveStatus ParseModelsFeedResponse(string body, string expectedUsername)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("blocks", out var blocks)
            || blocks.ValueKind != JsonValueKind.Array)
        {
            return new PandaLiveStatus { Success = false, Message = "모델 피드 응답 형식이 올바르지 않습니다." };
        }

        foreach (var block in blocks.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object
                || !block.TryGetProperty("models", out var models)
                || models.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var model in models.EnumerateArray())
            {
                var candidateUsername = GetString(model, "username");
                if (!candidateUsername.Equals(expectedUsername, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return BuildStatusFromModel(model, candidateUsername);
            }
        }

        // Not found in any block the account currently sees. Structurally this covers both
        // "genuinely offline" and "not popular/favorited enough to be ranked in" - either way,
        // scheduling this like a routine offline reading (rather than a hard failure) keeps the
        // model on the normal recheck cadence instead of sliding into exponential backoff.
        return new PandaLiveStatus
        {
            Success = true,
            IsLive = false,
            Message = "오프라인 (피드에서 확인 안됨 - 스챗에서 즐겨찾기하면 더 안정적으로 잡힙니다)"
        };
    }

    private static PandaLiveStatus BuildStatusFromModel(JsonElement model, string username)
    {
        var isLive = GetBoolean(model, "isLive");
        var status = GetString(model, "status");
        var streamName = GetString(model, "streamName");
        if (string.IsNullOrWhiteSpace(streamName))
        {
            streamName = GetString(model, "id");
        }

        var width = 0;
        var height = 0;
        if (model.TryGetProperty("broadcastSettings", out var broadcastSettings) && broadcastSettings.ValueKind == JsonValueKind.Object)
        {
            width = GetInt(broadcastSettings, "width");
            height = GetInt(broadcastSettings, "height");
        }

        if (!isLive)
        {
            return new PandaLiveStatus { Success = true, IsLive = false, Message = "오프라인", Nickname = username };
        }

        // "public" is a free room; anything else (e.g. "groupShow") needs a paid ticket. Mirrors
        // pandalive's own ticketed-room shape - Success=false with a "풀방 입장권" message - so
        // PaidRoomRetryRule's 10s recheck applies here too without any interval-rule changes.
        if (!status.Equals("public", StringComparison.OrdinalIgnoreCase))
        {
            return new PandaLiveStatus
            {
                Success = false,
                Message = "풀방 입장권이 필요합니다 (그룹쇼)",
                Nickname = username
            };
        }

        if (string.IsNullOrWhiteSpace(streamName))
        {
            return new PandaLiveStatus { Success = false, Message = "스트림 이름을 확인할 수 없습니다.", Nickname = username };
        }

        // preferredVideoCodec=h264, not av1: RecordingService stream-copies straight into an
        // .ts (MPEG-TS) container (-c copy -f mpegts), and AV1-in-MPEG-TS is not something ffmpeg's
        // muxer handles reliably. Switching this alone did not fix the "exits at code 0 within a
        // second, no ffmpeg warnings at all" symptom though, which is the next, stronger lead:
        // zero stderr output means ffmpeg never complained - it just read what looked like a
        // complete/short segment list and stopped, exactly what happens when a classic (non-LL-
        // aware) HLS puller is handed an LL-HLS "lowLatency" playlist. LL-HLS expects the client to
        // add _HLS_msn/_HLS_part on each poll to block for new partial segments; ffmpeg's demuxer
        // doesn't do that, so it can end up treating the playlist as finished instead of live.
        // Dropping playlistType entirely falls back to the CDN's standard (non-LL) delivery, which
        // is what a classic puller like ffmpeg actually expects.
        var masterPlaylistUrl =
            $"https://edge-hls.doppiocdn.org/hls/{Uri.EscapeDataString(streamName)}/master/{Uri.EscapeDataString(streamName)}_auto.m3u8"
            + "?preferredVideoCodec=h264";

        return new PandaLiveStatus
        {
            Success = true,
            IsLive = true,
            Message = "방송중",
            Nickname = username,
            Width = width,
            Height = height,
            StreamUrl = masterPlaylistUrl
        };
    }

    private static async Task EnsureStripchatOriginAsync(WebView2 webView, CancellationToken cancellationToken)
    {
        var currentUrl = webView.Source?.ToString() ?? "";
        if (currentUrl.Contains("stripchat.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            completion.TrySetResult();
        }

        webView.CoreWebView2.NavigationCompleted += Handler;
        try
        {
            webView.CoreWebView2.Navigate(StripchatHomeUri.ToString());
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        finally
        {
            webView.CoreWebView2.NavigationCompleted -= Handler;
        }
    }

    private static async Task NavigateAndWaitAsync(WebView2 webView, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            completion.TrySetResult();
        }

        webView.CoreWebView2.NavigationCompleted += Handler;
        try
        {
            webView.CoreWebView2.Navigate(url);
            await completion.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            webView.CoreWebView2.NavigationCompleted -= Handler;
        }
    }

    private static async Task<string> BuildCookieHeaderAsync(WebView2 webView)
    {
        var cookieManager = webView.CoreWebView2.CookieManager;
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var cookie in await cookieManager.GetCookiesAsync(StripchatHomeUri.ToString()))
        {
            if (!string.IsNullOrWhiteSpace(cookie.Name))
            {
                cookies[cookie.Name] = cookie.Value;
            }
        }

        return string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}"));
    }

    private static int CountCookiePairs(string cookieHeader)
    {
        return string.IsNullOrWhiteSpace(cookieHeader)
            ? 0
            : cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static async Task<string> GetUserAgentAsync(WebView2 webView)
    {
        try
        {
            var raw = await webView.CoreWebView2.ExecuteScriptAsync("navigator.userAgent");
            return JsonSerializer.Deserialize<string>(raw) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return "";
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? "",
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => ""
        };
    }
}
