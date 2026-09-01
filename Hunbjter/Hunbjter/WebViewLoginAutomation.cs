using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

public sealed class WebViewLoginAutomation : ILoginAutomation
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly WebView2 webView;

    public WebViewLoginAutomation(WebView2 webView)
    {
        this.webView = webView;
    }

    public async Task<LoginAutomationResult> LoginAsync(LoginSettings settings, string password, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.LoginUrl.Trim(), UriKind.Absolute, out var loginUri))
        {
            return new LoginAutomationResult(false, false, false, "URL을 확인하세요.");
        }

        await WebViewProfile.EnsureCoreAsync(webView);

        var completion = new TaskCompletionSource<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs>();

        void Handler(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            completion.TrySetResult(e);
        }

        webView.CoreWebView2.NavigationCompleted += Handler;
        try
        {
            webView.CoreWebView2.Navigate(loginUri.ToString());
            await using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            var navigation = await completion.Task;

            if (!navigation.IsSuccess)
            {
                return new LoginAutomationResult(false, false, false, $"페이지 로드 실패: {navigation.WebErrorStatus}");
            }

            var script = BuildLoginScript(settings, password);
            var rawResult = await webView.CoreWebView2.ExecuteScriptAsync(script);
            var result = JsonSerializer.Deserialize<ScriptLoginResult>(rawResult, JsonOptions) ?? new ScriptLoginResult();

            var message = result switch
            {
                { UserOk: true, PasswordOk: true, SubmitOk: true, OpenedLoginPanel: true } => "로그인 창을 열고 로그인 버튼을 클릭했습니다.",
                { UserOk: true, PasswordOk: true, SubmitOk: true } => "로그인 버튼을 클릭했습니다.",
                { TopLoginFound: false } => "상단 로그인/회원가입 버튼을 찾지 못했습니다.",
                { TopLoginFound: true, TopLoginClicked: false } => "상단 로그인/회원가입 버튼 클릭에 실패했습니다.",
                { PanelLoginFound: false } => "드롭다운 패널의 로그인 버튼을 찾지 못했습니다.",
                { PanelLoginFound: true, PanelLoginClicked: false } => "드롭다운 패널의 로그인 버튼 클릭에 실패했습니다.",
                { UserOk: false, OpenedLoginPanel: true } => "로그인 창은 열었지만 아이디 입력칸을 찾지 못했습니다.",
                { UserOk: false } => "아이디 입력칸을 찾지 못했습니다.",
                { PasswordOk: false } => "비밀번호 입력칸을 찾지 못했습니다.",
                { UserOk: true, PasswordOk: true, SubmitEnabled: false } => "아이디/비밀번호는 입력했지만 로그인 버튼이 활성화되지 않았습니다.",
                { SubmitOk: false } => "로그인 버튼 또는 폼을 찾지 못했습니다.",
                _ => "자동 입력 결과를 확인하세요."
            };

            return new LoginAutomationResult(result.UserOk, result.PasswordOk, result.SubmitOk, message);
        }
        finally
        {
            webView.CoreWebView2.NavigationCompleted -= Handler;
        }
    }

    private static string BuildLoginScript(LoginSettings settings, string password)
    {
        return $$"""
            (async () => {
                const userId = {{ToJs(settings.UserId)}};
                const password = {{ToJs(password)}};
                const pandaTopLoginSelector = '#radix-_R_3bj5uauivb_ > div';
                const pandaPanelLoginSelector = '#radix-_R_3bj5uauivbH1_ > div > div.px-\\[8px\\].py-\\[16px\\] > div > button';
                let openedLoginPanel = false;
                let topLoginFound = false;
                let topLoginClicked = false;
                let panelLoginFound = false;
                let panelLoginClicked = false;
                let loginTabClicked = false;

                const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));

                const query = (selector, root = document) => {
                    if (!selector) {
                        return null;
                    }

                    try {
                        return root.querySelector(selector);
                    } catch {
                        return null;
                    }
                };

                const visible = (element) => {
                    if (!element) {
                        return false;
                    }

                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
                };

                const textOf = (element) => `${element.innerText || ''} ${element.textContent || ''} ${element.value || ''}`.replace(/\s+/g, ' ').trim();
                const clickables = () => Array.from(document.querySelectorAll('button, a, input[type="button"], input[type="submit"], [role="button"], [onclick]')).filter(visible);
                const visibleElements = () => Array.from(document.querySelectorAll('button, a, div, span, [role="button"], [role="tab"], [onclick]')).filter(visible);
                const inputs = () => Array.from(document.querySelectorAll('input')).filter(visible);

                const clickLikeUser = (element) => {
                    if (!element) {
                        return false;
                    }

                    element.scrollIntoView({ block: 'center', inline: 'center' });
                    const rect = element.getBoundingClientRect();
                    const options = {
                        bubbles: true,
                        cancelable: true,
                        view: window,
                        clientX: rect.left + rect.width / 2,
                        clientY: rect.top + rect.height / 2
                    };

                    element.dispatchEvent(new PointerEvent('pointerover', options));
                    element.dispatchEvent(new MouseEvent('mouseover', options));
                    element.dispatchEvent(new PointerEvent('pointerenter', options));
                    element.dispatchEvent(new MouseEvent('mouseenter', options));
                    element.dispatchEvent(new PointerEvent('pointerdown', options));
                    element.dispatchEvent(new MouseEvent('mousedown', options));
                    element.focus();
                    element.dispatchEvent(new PointerEvent('pointerup', options));
                    element.dispatchEvent(new MouseEvent('mouseup', options));
                    element.dispatchEvent(new MouseEvent('click', options));
                    element.click();
                    return true;
                };

                const findByText = (patterns, root = document, exclude = []) => {
                    const excluded = new Set(exclude.filter(Boolean));
                    return Array.from(root.querySelectorAll('button, a, input[type="button"], input[type="submit"], [role="button"], [onclick]')).find(element => {
                        return !excluded.has(element) && visible(element) && patterns.some(pattern => textOf(element).includes(pattern));
                    });
                };

                const waitForTopLogin = async () => {
                    for (let i = 0; i < 40; i++) {
                        const pandaTopLogin = query(pandaTopLoginSelector);
                        if (visible(pandaTopLogin)) {
                            return pandaTopLogin;
                        }

                        const exact = clickables().find(element => {
                            const text = textOf(element).replace(/\s+/g, '');
                            return text.includes('로그인/회원가입');
                        });

                        if (exact) {
                            return exact;
                        }

                        // The embedded WebView2 pane is narrower than a normal browser window, so
                        // the site's own responsive layout can drop down to a breakpoint where this
                        // button's actual DOM text is just "회원가입" with "로그인" gone entirely
                        // (not just visually clipped) - match either half, since clicking either
                        // opens the same modal with both 로그인/회원가입 tabs.
                        const fallback = clickables()
                            .filter(element => {
                                const rect = element.getBoundingClientRect();
                                const text = textOf(element);
                                return (text.includes('로그인') || text.includes('회원가입'))
                                    && rect.top < 90
                                    && rect.right > window.innerWidth - 260;
                            })
                            .sort((a, b) => {
                                const ar = a.getBoundingClientRect();
                                const br = b.getBoundingClientRect();
                                return br.right - ar.right || ar.top - br.top;
                            })[0];

                        if (fallback) {
                            return fallback;
                        }

                        await sleep(250);
                    }

                    return null;
                };

                const waitForPasswordInput = async () => {
                    for (let i = 0; i < 24; i++) {
                        const passwordInput = inputs().find(input => {
                            const type = (input.getAttribute('type') || '').toLowerCase();
                            const name = (input.getAttribute('name') || '').toLowerCase();
                            const placeholder = input.getAttribute('placeholder') || '';
                            return type === 'password' || name.includes('pw') || name.includes('password') || placeholder.includes('비밀번호');
                        });

                        if (passwordInput) {
                            return passwordInput;
                        }

                        await sleep(250);
                    }

                    return null;
                };

                const clickLoginTabIfPresent = async () => {
                    for (let i = 0; i < 12; i++) {
                        const firstInput = inputs()
                            .sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top)[0];
                        const inputTop = firstInput?.getBoundingClientRect().top ?? Number.POSITIVE_INFINITY;

                        const candidates = visibleElements()
                            .filter(element => {
                                const text = textOf(element).replace(/\s+/g, '');
                                if (text !== '로그인') {
                                    return false;
                                }

                                const rect = element.getBoundingClientRect();
                                const parentText = textOf(element.parentElement || element).replace(/\s+/g, '');
                                return rect.width >= 40
                                    && rect.height >= 24
                                    && rect.top < inputTop - 12
                                    && rect.left < window.innerWidth - 160
                                    && parentText.includes('회원가입');
                            })
                            .sort((a, b) => {
                                const ar = a.getBoundingClientRect();
                                const br = b.getBoundingClientRect();
                                return ar.top - br.top || ar.left - br.left;
                            });

                        if (candidates[0]) {
                            loginTabClicked = clickLikeUser(candidates[0]);
                            await sleep(500);
                            return loginTabClicked;
                        }

                        await sleep(250);
                    }

                    return false;
                };

                const waitForPanelLogin = async (topLogin) => {
                    const topRect = topLogin?.getBoundingClientRect();
                    for (let i = 0; i < 24; i++) {
                        const pandaPanelLogin = query(pandaPanelLoginSelector);
                        if (visible(pandaPanelLogin)) {
                            return pandaPanelLogin;
                        }

                        const candidates = clickables()
                            .filter(element => element !== topLogin && textOf(element).trim() === '로그인')
                            .filter(element => {
                                const rect = element.getBoundingClientRect();
                                if (!topRect) {
                                    return rect.top < 180 && rect.right > window.innerWidth - 260;
                                }

                                return rect.top >= topRect.bottom - 8
                                    && rect.top < topRect.bottom + 120
                                    && rect.left >= topRect.left - 220
                                    && rect.right <= topRect.right + 80;
                            })
                            .sort((a, b) => {
                                const ar = a.getBoundingClientRect();
                                const br = b.getBoundingClientRect();
                                return br.right - ar.right || ar.top - br.top;
                            });

                        if (candidates[0]) {
                            return candidates[0];
                        }

                        await sleep(250);
                    }

                    return findByText(['로그인'], document, [topLogin]);
                };

                const openLoginModal = async () => {
                    const topLogin = await waitForTopLogin();
                    if (topLogin) {
                        topLoginFound = true;
                        topLoginClicked = clickLikeUser(topLogin);
                        await sleep(800);
                    }

                    let passwordInput = await waitForPasswordInput();
                    if (passwordInput) {
                        openedLoginPanel = true;
                        return passwordInput;
                    }

                    const menuLogin = await waitForPanelLogin(topLogin);
                    if (menuLogin) {
                        panelLoginFound = true;
                        panelLoginClicked = clickLikeUser(menuLogin);
                        openedLoginPanel = true;
                        await sleep(800);
                    }

                    await clickLoginTabIfPresent();
                    return await waitForPasswordInput();
                };

                let passwordInput = await waitForPasswordInput();
                if (passwordInput) {
                    await clickLoginTabIfPresent();
                    passwordInput = await waitForPasswordInput();
                }

                if (!passwordInput) {
                    passwordInput = await openLoginModal();
                }

                const findUserInput = () => {
                    if (!passwordInput) {
                        return null;
                    }

                    const passwordRect = passwordInput.getBoundingClientRect();
                    return inputs()
                        .filter(input => input !== passwordInput && (input.getAttribute('type') || 'text').toLowerCase() !== 'password')
                        .filter(input => {
                            const rect = input.getBoundingClientRect();
                            return rect.bottom <= passwordRect.top + 8 && Math.abs(rect.left - passwordRect.left) < 80;
                        })
                        .sort((a, b) => b.getBoundingClientRect().top - a.getBoundingClientRect().top)[0]
                        || inputs().find(input => input !== passwordInput && (input.getAttribute('type') || 'text').toLowerCase() !== 'password');
                };

                const userInput = findUserInput();

                const inputNativeValue = (element, value) => {
                    if (!element) {
                        return false;
                    }

                    const prototype = Object.getPrototypeOf(element);
                    const descriptor = Object.getOwnPropertyDescriptor(prototype, 'value');
                    if (descriptor && descriptor.set) {
                        descriptor.set.call(element, value);
                    } else {
                        element.value = value;
                    }

                    return true;
                };

                const typeLikeUser = async (element, value) => {
                    if (!element) {
                        return false;
                    }

                    element.focus();
                    clickLikeUser(element);
                    inputNativeValue(element, '');
                    element.dispatchEvent(new Event('input', { bubbles: true }));

                    for (const char of value) {
                        element.dispatchEvent(new KeyboardEvent('keydown', { key: char, bubbles: true, cancelable: true }));
                        element.dispatchEvent(new InputEvent('beforeinput', { inputType: 'insertText', data: char, bubbles: true, cancelable: true }));
                        inputNativeValue(element, element.value + char);
                        element.dispatchEvent(new InputEvent('input', { inputType: 'insertText', data: char, bubbles: true }));
                        element.dispatchEvent(new KeyboardEvent('keyup', { key: char, bubbles: true, cancelable: true }));
                        await sleep(15);
                    }

                    element.dispatchEvent(new Event('change', { bubbles: true }));
                    element.blur();
                    return true;
                };

                const waitForEnabled = async (button) => {
                    if (!button) {
                        return false;
                    }

                    for (let i = 0; i < 20; i++) {
                        const disabled = button.disabled || button.getAttribute('disabled') !== null || button.getAttribute('aria-disabled') === 'true';
                        const style = window.getComputedStyle(button);
                        const pointerBlocked = style.pointerEvents === 'none';
                        if (!disabled && !pointerBlocked) {
                            return true;
                        }

                        await sleep(100);
                    }

                    return false;
                };

                const findSubmit = () => {
                    const passwordRect = passwordInput?.getBoundingClientRect();
                    const candidates = clickables().filter(element => {
                        const rect = element.getBoundingClientRect();
                        return textOf(element).includes('로그인')
                            && (!passwordRect || (rect.top > passwordRect.top && Math.abs(rect.left - passwordRect.left) < 160));
                    });

                    return candidates.sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top)[0]
                        || query('button[type="submit"], input[type="submit"]')
                        || findByText(['로그인']);
                };

                let userOk = await typeLikeUser(userInput, userId);
                const passwordOk = await typeLikeUser(passwordInput, password);

                if (userInput && userInput.value !== userId) {
                    userOk = await typeLikeUser(userInput, userId);
                    if (passwordInput && passwordInput.value !== password) {
                        await typeLikeUser(passwordInput, password);
                    }
                }

                await sleep(200);

                const submitButton = findSubmit();
                let submitOk = false;
                const submitEnabled = await waitForEnabled(submitButton);

                if (submitEnabled && submitButton && submitButton !== userInput && submitButton !== passwordInput) {
                    clickLikeUser(submitButton);
                    submitOk = true;
                } else if (submitButton && !submitEnabled) {
                    submitOk = false;
                } else if (passwordInput && passwordInput.form) {
                    passwordInput.form.requestSubmit();
                    submitOk = true;
                }

                return { userOk, passwordOk, submitOk, submitEnabled, openedLoginPanel, topLoginFound, topLoginClicked, panelLoginFound, panelLoginClicked, loginTabClicked };
            })();
            """;
    }

    private static string ToJs(string value)
    {
        return JsonSerializer.Serialize(value.Trim());
    }

    private sealed class ScriptLoginResult
    {
        public bool UserOk { get; set; }

        public bool PasswordOk { get; set; }

        public bool SubmitOk { get; set; }

        public bool SubmitEnabled { get; set; }

        public bool OpenedLoginPanel { get; set; }

        public bool TopLoginFound { get; set; }

        public bool TopLoginClicked { get; set; }

        public bool PanelLoginFound { get; set; }

        public bool PanelLoginClicked { get; set; }

        public bool LoginTabClicked { get; set; }
    }
}
