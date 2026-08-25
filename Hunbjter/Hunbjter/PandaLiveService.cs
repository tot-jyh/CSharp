using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

public sealed class PandaLiveService
{
    // One shared client: a per-call HttpClient leaks sockets in TIME_WAIT, which matters as
    // soon as checks run per model instead of once per batch. RecordingService.PlaylistClient
    // already follows this pattern.
    private static readonly HttpClient LivePlayClient = new();

    private static readonly Uri PandaHomeUri = new("https://www.pandalive.co.kr/");
    private static readonly Uri PandaLivePlayApiUri = new("https://api.pandalive.co.kr/v1/live/play");

    public async Task<RecordingHttpContext> GetRecordingHttpContextAsync(WebView2 webView, CancellationToken cancellationToken = default)
    {
        await WebViewProfile.EnsureCoreAsync(webView);
        var userAgent = await GetUserAgentAsync(webView);
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = "Mozilla/5.0";
        }

        var cookieHeader = await BuildCookieHeaderAsync(webView);
        return new RecordingHttpContext(
            userAgent,
            BuildRecordingHeaderText(cookieHeader),
            CountCookiePairs(cookieHeader));
    }

    public async Task<PandaSessionStatus> GetSessionStatusAsync(WebView2 webView, CancellationToken cancellationToken = default)
    {
        await WebViewProfile.EnsureCoreAsync(webView);

        try
        {
            await EnsurePandaOriginAsync(webView, cancellationToken);
        }
        catch
        {
            // A partially loaded Panda page can still expose cookies; keep checking below.
        }

        var cookieHeader = await BuildCookieHeaderAsync(webView);
        var viewerUserIndex = await GetViewerUserIndexAsync(webView);
        return new PandaSessionStatus(
            !string.IsNullOrWhiteSpace(cookieHeader),
            CountCookiePairs(cookieHeader),
            !string.IsNullOrWhiteSpace(viewerUserIndex));
    }

    public async Task<PandaSessionStatus> PrepareSessionAsync(WebView2 webView, CancellationToken cancellationToken = default)
    {
        await WebViewProfile.EnsureCoreAsync(webView);
        await NavigateAndWaitAsync(webView, PandaHomeUri.ToString(), TimeSpan.FromSeconds(20), cancellationToken);
        await Task.Delay(800, cancellationToken);
        return await GetSessionStatusAsync(webView, cancellationToken);
    }

    public async Task<PandaLiveStatus> GetLiveStatusAsync(WebView2 webView, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new PandaLiveStatus { Success = false, Message = "아이디가 비어 있습니다." };
        }

        userId = userId.Trim();
        await WebViewProfile.EnsureCoreAsync(webView);

        // HTTP-first: this reuses the shared WebView2's cookies/user-index without an actual
        // page navigation, so a check finishes in well under a second instead of a Navigate()
        // + DevTools round trip. EnsurePandaOriginAsync is a no-op once the WebView2 has ever
        // visited pandalive, so it stays cheap on every later call.
        string? httpFailureMessage = null;
        try
        {
            await EnsurePandaOriginAsync(webView, cancellationToken);
            var body = await RequestLivePlayByHttpAsync(webView, userId, cancellationToken);
            var httpStatus = ParseLivePlayResponse(body, userId);

            // pandalive answers an unauthenticated/unverified request with HTTP 200 and a
            // message like "본인인증이 필요합니다" rather than an error status - no exception
            // is thrown, so a plain try/catch here would report this as final. The x-device-info
            // "ui" value this HTTP path relies on is scraped from the WebView2's JS state and is
            // not always reliably present, whereas the browser-navigation fallback below doesn't
            // need it at all (the real page makes its own request with its own headers). So a
            // session-shaped failure here falls through to that fallback too, not just a thrown
            // exception, to keep the same resilience the browser-first order used to have.
            if (httpStatus.Success || !PandaMessages.IsSessionRelatedFailure(httpStatus.Message))
            {
                return httpStatus;
            }

            httpFailureMessage = httpStatus.Message;
        }
        catch (Exception httpEx)
        {
            httpFailureMessage = httpEx.Message;
        }

        try
        {
            var body = await RequestLivePlayThroughNetworkAsync(webView, userId, cancellationToken);
            return ParseLivePlayResponse(body, userId);
        }
        catch (Exception browserEx)
        {
            return new PandaLiveStatus
            {
                Success = false,
                Message = $"팬더 확인 실패: HTTP {httpFailureMessage}; 브라우저 {browserEx.Message}"
            };
        }
    }

    // The page runs its API request in a separate execution context. DevTools sees that
    // request reliably, and the response body must be read as soon as it is received.
    private static async Task<string> RequestLivePlayThroughNetworkAsync(WebView2 webView, string userId, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestReceiver = webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.requestWillBeSent");
        var responseReceiver = webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.responseReceived");
        var targetRequestIds = new HashSet<string>(StringComparer.Ordinal);

        void RequestHandler(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            try
            {
                using var document = JsonDocument.Parse(e.ParameterObjectAsJson);
                var root = document.RootElement;
                if (!root.TryGetProperty("request", out var request)
                    || !IsLivePlayRequestForUser(request, userId))
                {
                    return;
                }

                var requestId = GetString(root, "requestId");
                if (!string.IsNullOrWhiteSpace(requestId))
                {
                    targetRequestIds.Add(requestId);
                }
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }

        static bool IsLivePlayRequestForUser(JsonElement request, string expectedUserId)
        {
            var url = GetString(request, "url");
            var method = GetString(request, "method");
            if (!url.Contains("api.pandalive.co.kr/v1/live/play", StringComparison.OrdinalIgnoreCase)
                || !method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return RequestBodyContainsUserId(GetString(request, "postData"), expectedUserId);
        }
        async void ResponseHandler(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            try
            {
                using var document = JsonDocument.Parse(e.ParameterObjectAsJson);
                var root = document.RootElement;
                var requestId = GetString(root, "requestId");
                if (string.IsNullOrWhiteSpace(requestId) || !targetRequestIds.Contains(requestId))
                {
                    return;
                }

                var wrappedBody = await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Network.getResponseBody",
                    JsonSerializer.Serialize(new { requestId }));
                completion.TrySetResult(UnwrapDevToolsBody(wrappedBody));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }

        requestReceiver.DevToolsProtocolEventReceived += RequestHandler;
        responseReceiver.DevToolsProtocolEventReceived += ResponseHandler;
        try
        {
            await using var cancellation = cancellationToken.Register(
                static state => ((TaskCompletionSource<string>)state!).TrySetCanceled(), completion);

            await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            webView.CoreWebView2.Navigate($"https://www.pandalive.co.kr/play/{Uri.EscapeDataString(userId)}");
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        finally
        {
            requestReceiver.DevToolsProtocolEventReceived -= RequestHandler;
            responseReceiver.DevToolsProtocolEventReceived -= ResponseHandler;

            try
            {
                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.disable", "{}");
            }
            catch
            {
                // Network cleanup is best-effort.
            }
        }
    }

    private static bool RequestBodyContainsUserId(string body, string expectedUserId)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(expectedUserId))
        {
            return false;
        }

        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var splitAt = part.IndexOf('=');
            if (splitAt <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..splitAt].Replace("+", " "));
            if (!key.Equals("userId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(part[(splitAt + 1)..].Replace("+", " "));
            return value.Equals(expectedUserId, StringComparison.OrdinalIgnoreCase);
        }

        return body.Contains($"\"userId\":\"{expectedUserId}\"", StringComparison.OrdinalIgnoreCase)
            || body.Contains($"\"userId\": \"{expectedUserId}\"", StringComparison.OrdinalIgnoreCase);
    }
    private static async Task<string> RequestLivePlayThroughBrowserAsync(WebView2 webView, string userId)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var script = $$"""
            (() => {
                const expectedUserId = {{JsonSerializer.Serialize(userId)}};
                const hasExpectedUserId = (body) => {
                    const text = String(body || '');
                    if (!text) return false;
                    try {
                        const params = new URLSearchParams(text);
                        if ((params.get('userId') || '').toLowerCase() === expectedUserId.toLowerCase()) return true;
                    } catch {}
                    return text.toLowerCase().includes(`"userid":"${expectedUserId.toLowerCase()}"`)
                        || text.toLowerCase().includes(`"userid": "${expectedUserId.toLowerCase()}"`);
                };
                const isTarget = (url, method, body) =>
                    String(url || '').includes('/v1/live/play')
                    && String(method || 'GET').toUpperCase() === 'POST'
                    && hasExpectedUserId(body);

                const report = (response) => {
                    response.clone().text()
                        .then(body => window.chrome.webview.postMessage({ type: 'panda-live-play', body }))
                        .catch(error => window.chrome.webview.postMessage({ type: 'panda-live-play-error', message: String(error) }));
                };

                const originalFetch = window.fetch;
                window.fetch = async function(input, init) {
                    const response = await originalFetch.apply(this, arguments);
                    const request = typeof input === 'string' ? null : input;
                    const url = typeof input === 'string' ? input : request?.url;
                    const method = init?.method || request?.method;
                    const body = init?.body || '';
                    if (isTarget(url, method, body)) {
                        report(response);
                    }
                    return response;
                };

                const OriginalXhr = window.XMLHttpRequest;
                window.XMLHttpRequest = function() {
                    const xhr = new OriginalXhr();
                    let method = 'GET';
                    let url = '';
                    const open = xhr.open;
                    const send = xhr.send;
                    xhr.open = function(nextMethod, nextUrl) {
                        method = nextMethod;
                        url = nextUrl;
                        return open.apply(this, arguments);
                    };
                    xhr.send = function(body) {
                        if (isTarget(url, method, body)) {
                            xhr.addEventListener('loadend', () => {
                                window.chrome.webview.postMessage({ type: 'panda-live-play', body: xhr.responseText || '' });
                            }, { once: true });
                        }
                        return send.apply(this, arguments);
                    };
                    return xhr;
                };
            })();
            """;

        void WebMessageHandler(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var document = JsonDocument.Parse(e.WebMessageAsJson);
                var root = document.RootElement;
                var type = GetString(root, "type");
                if (type == "panda-live-play")
                {
                    var body = GetString(root, "body");
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        completion.TrySetException(new InvalidOperationException("팬더 API 응답 본문이 비어 있습니다."));
                    }
                    else
                    {
                        completion.TrySetResult(body);
                    }
                }
                else if (type == "panda-live-play-error")
                {
                    completion.TrySetException(new InvalidOperationException(GetString(root, "message")));
                }
            }
            catch
            {
                // Ignore unrelated messages from the embedded page.
            }
        }

        webView.CoreWebView2.WebMessageReceived += WebMessageHandler;
        var scriptId = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        try
        {
            var playUrl = $"https://www.pandalive.co.kr/play/{Uri.EscapeDataString(userId)}";
            webView.CoreWebView2.Navigate(playUrl);

            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            webView.CoreWebView2.WebMessageReceived -= WebMessageHandler;

            try
            {
                webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(scriptId);
            }
            catch
            {
                // The browser can finish while its document-created hook is being removed.
            }
        }
    }

    private static async Task EnsurePandaOriginAsync(WebView2 webView, CancellationToken cancellationToken)
    {
        var currentUrl = webView.Source?.ToString() ?? "";
        if (currentUrl.Contains("pandalive.co.kr", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var completion = new TaskCompletionSource();
        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            completion.TrySetResult();
        }

        webView.CoreWebView2.NavigationCompleted += Handler;
        try
        {
            webView.CoreWebView2.Navigate(PandaHomeUri.ToString());
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

    private static async Task<string> RequestLivePlayByHttpAsync(WebView2 webView, string userId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PandaLivePlayApiUri);

        var cookieHeader = await BuildCookieHeaderAsync(webView);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        var userAgent = await GetUserAgentAsync(webView);
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }

        var viewerUserIndex = await GetViewerUserIndexAsync(webView);
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "ko");
        request.Headers.TryAddWithoutValidation("Origin", PandaHomeUri.GetLeftPart(UriPartial.Authority));
        request.Headers.TryAddWithoutValidation("Referer", PandaHomeUri.ToString());
        request.Headers.TryAddWithoutValidation(
            "x-device-info",
            JsonSerializer.Serialize(new
            {
                t = "webPc",
                v = "1.0",
                ui = viewerUserIndex,
                ck = new { sessKeyAsp = "" }
            }));

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userId"] = userId,
            ["action"] = "watch"
        });

        using var response = await LivePlayClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var uiText = string.IsNullOrWhiteSpace(viewerUserIndex) ? "ui 없음" : $"ui {viewerUserIndex}";
            throw new InvalidOperationException($"API HTTP {(int)response.StatusCode} ({uiText}): {TrimForLog(body)}");
        }

        return body;
    }

    private static string UnwrapDevToolsBody(string wrappedBody)
    {
        using var document = JsonDocument.Parse(wrappedBody);
        var root = document.RootElement;
        var body = GetString(root, "body");
        var base64Encoded = GetBoolean(root, "base64Encoded");

        if (!base64Encoded)
        {
            return body;
        }

        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(body));
    }

    private static async Task<string> GetViewerUserIndexAsync(WebView2 webView)
    {
        try
        {
            var raw = await webView.CoreWebView2.ExecuteScriptAsync(
                """
                (() => {
                    const seen = new Set();
                    const findIdx = (value) => {
                        if (!value || typeof value !== 'object' || seen.has(value)) {
                            return '';
                        }

                        seen.add(value);

                        if (value.userInfo && value.userInfo.idx) {
                            return String(value.userInfo.idx);
                        }

                        if (value.loginInfo && value.loginInfo.userInfo && value.loginInfo.userInfo.idx) {
                            return String(value.loginInfo.userInfo.idx);
                        }

                        if (value.idx && (value.id || value.nick || value.authYN || value.isAdult !== undefined)) {
                            return String(value.idx);
                        }

                        for (const child of Object.values(value)) {
                            const found = findIdx(child);
                            if (found) {
                                return found;
                            }
                        }

                        return '';
                    };

                    const scanText = (text) => {
                        if (!text) {
                            return '';
                        }

                        const patterns = [
                            /"idx"\s*:\s*(\d+)/,
                            /"userIdx"\s*:\s*(\d+)/,
                            /idx['"]?\s*:\s*(\d+)/,
                            /userIdx['"]?\s*:\s*(\d+)/
                        ];

                        for (const pattern of patterns) {
                            const match = String(text).match(pattern);
                            if (match) {
                                return match[1];
                            }
                        }

                        return '';
                    };

                    const scanStorage = (storage) => {
                        for (let i = 0; i < storage.length; i++) {
                            const key = storage.key(i);
                            const text = storage.getItem(key) || '';
                            try {
                                const found = findIdx(JSON.parse(text));
                                if (found) {
                                    return found;
                                }
                            } catch {
                                const found = scanText(text);
                                if (found) {
                                    return found;
                                }
                            }
                        }

                        return '';
                    };

                    const scanNextData = () => {
                        const next = document.querySelector('#__NEXT_DATA__');
                        return scanText(next?.textContent || '');
                    };

                    const scanWindow = () => {
                        const keys = Object.keys(window).filter(key => /user|login|auth|member|store|panda|zustand|redux|next/i.test(key));
                        for (const key of keys) {
                            try {
                                const found = findIdx(window[key]) || scanText(JSON.stringify(window[key]));
                                if (found) {
                                    return found;
                                }
                            } catch {
                                // Ignore inaccessible or unserializable globals.
                            }
                        }

                        return '';
                    };

                    const scanHtml = () => scanText(document.documentElement.innerHTML);

                    return scanStorage(localStorage)
                        || scanStorage(sessionStorage)
                        || scanNextData()
                        || scanWindow()
                        || scanHtml()
                        || '';
                })();
                """);

            return JsonSerializer.Deserialize<string>(raw) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static async Task<string> BuildCookieHeaderAsync(WebView2 webView)
    {
        var cookieManager = webView.CoreWebView2.CookieManager;
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var url in new[] { PandaHomeUri.ToString(), PandaLivePlayApiUri.ToString() })
        {
            foreach (var cookie in await cookieManager.GetCookiesAsync(url))
            {
                if (!string.IsNullOrWhiteSpace(cookie.Name))
                {
                    cookies[cookie.Name] = cookie.Value;
                }
            }
        }

        return string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}"));
    }

    private static string BuildRecordingHeaderText(string cookieHeader)
    {
        var headers = new List<string>
        {
            "Referer: https://www.pandalive.co.kr/",
            "Origin: https://www.pandalive.co.kr"
        };

        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            headers.Add($"Cookie: {cookieHeader}");
        }

        return string.Join("\r\n", headers) + "\r\n";
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

    private static PandaLiveStatus ParseLivePlayResponse(string body, string expectedUserId)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var result = GetBoolean(root, "result");
        var message = GetString(root, "message");

        if (!root.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Object)
        {
            return new PandaLiveStatus
            {
                Success = result,
                IsLive = false,
                Message = string.IsNullOrWhiteSpace(message) ? "방송 정보가 없습니다." : message
            };
        }

        var isLive = GetBoolean(media, "isLive");
        var streamUrl = ExtractStreamUrl(root);
        var title = GetString(media, "title");
        var nickname = GetString(media, "userNick");
        var responseUserId = GetString(media, "userId");
        var width = GetInt(media, "sizeWidth");
        var height = GetInt(media, "sizeHeight");

        if (!string.IsNullOrWhiteSpace(responseUserId)
            && !responseUserId.Equals(expectedUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"응답 모델 불일치: 요청 {expectedUserId}, 응답 {responseUserId}");
        }

        return new PandaLiveStatus
        {
            Success = result,
            IsLive = isLive,
            Message = string.IsNullOrWhiteSpace(message) ? (isLive ? "방송중" : "오프라인") : message,
            UserId = responseUserId,
            Nickname = nickname,
            Title = title,
            Width = width,
            Height = height,
            StreamUrl = streamUrl
        };
    }

    private static string ExtractStreamUrl(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("PlayList", out var playlist)
            || playlist.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var key in new[] { "hls3", "hls2", "hls" })
        {
            if (!playlist.TryGetProperty(key, out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var url = GetString(entry, "url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }

        return "";
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
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property))
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

    private static string TrimForLog(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= 300 ? value : value[..300] + "...";
    }
}

public sealed record RecordingHttpContext(string UserAgent, string HeaderText, int CookieCount);

public sealed record PandaSessionStatus(bool IsLoggedIn, int CookieCount, bool HasViewerUserIndex);
