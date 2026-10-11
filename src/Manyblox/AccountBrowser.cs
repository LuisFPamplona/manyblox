using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Security.Cryptography;
using System.Text;

namespace Manyblox;

internal sealed class AccountBrowser : Form
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private readonly Label message = new() { Dock = DockStyle.Fill, AutoSize = false, Padding = new Padding(12), Text = "Preparando o login oficial…" };
    private readonly string dataDirectory;
    private readonly Uri initialPage;
    private Uri? pendingPage;
    private bool ready;
    private readonly AccountEntry? account;
    private readonly Action<AccountEntry>? saveAuthenticated;
    private readonly System.Windows.Forms.Timer loginTimer = new() { Interval = 1200 };
    private readonly CancellationTokenSource lifetime = new();
    private byte[]? lastSessionFingerprint;
    private bool checkingLogin;
    private readonly long playPlaceId;
    private readonly System.Windows.Forms.Timer playTimer = new() { Interval = 750 };
    private DateTime launchAuthorizedUntil;
    private bool clickingPlay;
    private bool playClicked;
    public event EventHandler? ClientLaunchRequested;
    public event EventHandler? ClientLaunchStarting;
    public event EventHandler? ClientLaunchFailed;

    public AccountBrowser(AccountEntry? account, string root, Uri page, Action<AccountEntry>? saveAuthenticated = null, long playPlaceId = 0)
    {
        this.account = account;
        this.saveAuthenticated = saveAuthenticated;
        this.playPlaceId = playPlaceId;
        Text = account is null ? "Adicionar conta · Login oficial Roblox — Manyblox" : $"Roblox · {account.Username} — Manyblox";
        Size = new Size(1100, 800);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        initialPage = page;
        // Fresh private profile for each session; never reuse personal Edge/Chrome data.
        dataDirectory = Path.Combine(root, "WebSessions", Guid.NewGuid().ToString("N"));
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 105, ColumnCount = 1, RowCount = 2 };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        AddAction(actions, "Página de login", () => Navigate(RobloxActions.LoginUri));
        AddAction(actions, "Início do Roblox", () => Navigate(new Uri("https://www.roblox.com/home")));
        AddAction(actions, "Recarregar", () =>
        {
            if (!ready) return;
            browser.Reload();
            lastSessionFingerprint = null;
            if (saveAuthenticated is not null) loginTimer.Start();
        });
        AddAction(actions, "Encerrar sessão", Close);
        top.Controls.Add(actions, 0, 0);
        top.Controls.Add(message, 0, 1);
        Controls.Add(browser);
        Controls.Add(top);
        Shown += async (_, _) => await InitializeAsync();
        loginTimer.Tick += async (_, _) => await CheckLoginAsync();
        playTimer.Tick += async (_, _) => await ClickOfficialPlayAsync();
        FormClosed += (_, _) => { loginTimer.Stop(); playTimer.Stop(); lifetime.Cancel(); };
    }

    private static void AddAction(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Padding = new Padding(6) };
        button.Click += (_, _) => action();
        parent.Controls.Add(button);
    }

    public void Navigate(Uri page)
    {
        if (!BrowserPolicy.IsOfficialPage(page.AbsoluteUri)) throw new InvalidDataException("Página não permitida.");
        if (ready) browser.CoreWebView2.Navigate(page.AbsoluteUri);
        else pendingPage = page;
    }

    private async Task InitializeAsync()
    {
        try
        {
            StateStore.SecureDirectory(dataDirectory);
            browser.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = dataDirectory,
                ProfileName = "Account_" + (account?.Id ?? Guid.NewGuid()).ToString("N"),
                IsInPrivateModeEnabled = true
            };
            await browser.EnsureCoreWebView2Async();
            if (IsDisposed) return;
            var core = browser.CoreWebView2;
            var settings = core.Settings;
            settings.AreDevToolsEnabled = false;
            settings.AreHostObjectsAllowed = false;
            settings.IsWebMessageEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => { e.Cancel = true; message.Text = "Download bloqueado. Instale o Roblox pelo site oficial fora do Manyblox."; };
            core.ServerCertificateErrorDetected += (_, e) => e.Action = CoreWebView2ServerCertificateErrorAction.Cancel;
            core.NavigationStarting += (_, e) =>
            {
                if (!BrowserPolicy.IsOfficialPage(e.Uri) &&
                    !BrowserPolicy.CanLaunchClient(e.Uri, core.Source, core.Source, e.IsUserInitiated || HasPlayAuthorization(core.Source)))
                {
                    e.Cancel = true;
                    message.Text = "Navegação fora do site oficial bloqueada.";
                }
            };
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (e.IsUserInitiated && BrowserPolicy.IsOfficialPage(e.Uri)) core.Navigate(e.Uri);
                else message.Text = "Janela externa bloqueada.";
            };
            core.LaunchingExternalUriScheme += (_, e) =>
            {
                e.Cancel = true;
                bool nativePlay = HasPlayAuthorization(core.Source);
                if (!BrowserPolicy.CanLaunchClient(e.Uri, e.InitiatingOrigin, core.Source, e.IsUserInitiated || nativePlay)) return;
                // Cancel the runtime's duplicate prompt. The native PLAY request
                // (or the explicit confirmation below) authorizes this single handoff.
                if (!nativePlay && MessageBox.Show(this,
                    "O site oficial pediu para abrir o cliente Roblox. Confira no site a conta autenticada. Abrir?",
                    "Abrir Roblox", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                launchAuthorizedUntil = DateTime.MinValue;
                playTimer.Stop();
                try
                {
                    ClientLaunchStarting?.Invoke(this, EventArgs.Empty);
                    RobloxActions.ForwardOfficialClientRequest(e.Uri, e.InitiatingOrigin, core.Source, true);
                    ClientLaunchRequested?.Invoke(this, EventArgs.Empty);
                    BeginInvoke(() => Close());
                }
                catch
                {
                    ClientLaunchFailed?.Invoke(this, EventArgs.Empty);
                    message.Text = "Não foi possível abrir o cliente. Verifique a instalação oficial do Roblox; nenhum ticket foi registrado.";
                }
            };
            core.NavigationCompleted += (_, e) => message.Text = e.IsSuccess
                ? saveAuthenticated is not null
                    ? "Entre no site oficial. Após confirmar o login, a sessão será salva com proteção Windows e esta janela fechará. A senha não é salva pelo Manyblox."
                    : $"Site oficial Roblox · {account?.Username}. Sessão salva; confira a conta no site antes de jogar."
                : "A página não carregou. Verifique a conexão e tente Recarregar; nenhuma proteção foi desativada.";
            core.ProcessFailed += (_, _) => { ready = false; message.Text = "O navegador interno parou. Feche esta sessão e abra novamente."; };
            ready = true;
            if (saveAuthenticated is null && account?.ProtectedSession.Length > 0)
            {
                string savedSession = SessionCredential.Unprotect(account.ProtectedSession);
                var cookie = core.CookieManager.CreateCookie(".ROBLOSECURITY", savedSession, ".roblox.com", "/");
                cookie.IsSecure = true;
                cookie.IsHttpOnly = true;
                core.CookieManager.AddOrUpdateCookie(cookie);
            }
            Navigate(pendingPage ?? initialPage);
            if (saveAuthenticated is not null) loginTimer.Start();
            if (playPlaceId > 0)
            {
                launchAuthorizedUntil = DateTime.UtcNow.AddSeconds(30);
                playTimer.Start();
            }
        }
        catch (WebView2RuntimeNotFoundException)
        {
            message.Text = "Instale o Microsoft Edge WebView2 Runtime oficial e abra a sessão novamente. Não há download automático.";
        }
        catch
        {
            message.Text = "Não foi possível iniciar o navegador interno. Feche esta janela e tente novamente. Nenhum dado privado foi registrado.";
        }
    }

    private bool HasPlayAuthorization(string source) => playClicked && DateTime.UtcNow <= launchAuthorizedUntil &&
        BrowserPolicy.IsGamePage(source, playPlaceId);

    private async Task ClickOfficialPlayAsync()
    {
        if (!ready || clickingPlay || IsDisposed || playPlaceId <= 0) return;
        if (DateTime.UtcNow > launchAuthorizedUntil)
        {
            playTimer.Stop();
            Show();
            message.Text = "O site não concluiu o lançamento automático. Confira a conta e use o botão Jogar oficial. Nenhum bloqueio será contornado.";
            return;
        }
        if (playClicked || !BrowserPolicy.IsGamePage(browser.CoreWebView2.Source, playPlaceId)) return;
        clickingPlay = true;
        try
        {
            // Fixed local script: click the official site's play button only.
            // No remote script downloads, form reading, cookies, tickets or arbitrary input.
            // The native PLAY click authorizes one protocol handoff for 30 seconds.
            playClicked = true;
            string result = await browser.CoreWebView2.ExecuteScriptAsync("""
                (() => {
                    const button = document.querySelector('#game-details-play-button-container .btn-play-game, #game-details-play-button-container .btn-common-play-game-lg, button[data-testid="play-button"]');
                    if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true') return false;
                    button.click();
                    return true;
                })()
                """);
            if (result != "true") playClicked = false;
        }
        catch
        {
            playTimer.Stop();
            launchAuthorizedUntil = DateTime.MinValue;
            if (!IsDisposed) Show();
            if (!IsDisposed) message.Text = "Não foi possível clicar em Jogar no site oficial. Use o botão do site; o cliente não foi alterado.";
        }
        finally { clickingPlay = false; }
    }

    private async Task CheckLoginAsync()
    {
        if (!ready || checkingLogin || IsDisposed || saveAuthenticated is null ||
            !BrowserPolicy.IsOfficialPage(browser.CoreWebView2.Source)) return;
        checkingLogin = true;
        try
        {
            var cookies = await browser.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com/");
            if (IsDisposed || lifetime.IsCancellationRequested) return;
            var cookie = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY" && c.IsSecure && c.IsHttpOnly &&
                c.Path == "/" && (c.Domain.TrimStart('.') == "roblox.com" || c.Domain == "www.roblox.com"));
            if (cookie is null) return;
            string session = cookie.Value;
            if (!SessionCredential.IsValidCookie(session)) return;
            byte[] bytes = Encoding.UTF8.GetBytes(session);
            byte[] fingerprint;
            try { fingerprint = SHA256.HashData(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            if (lastSessionFingerprint is not null && fingerprint.AsSpan().SequenceEqual(lastSessionFingerprint)) return;
            lastSessionFingerprint = fingerprint;
            using var api = new RobloxSessionApi();
            var identity = await api.IdentifyAsync(session, lifetime.Token);
            if (IsDisposed || lifetime.IsCancellationRequested) return;
            var confirmed = new AccountEntry(Guid.NewGuid(), identity.Username, "", "Pessoal", identity.Id, SessionCredential.Protect(session));
            saveAuthenticated(confirmed); // The database must commit before closing the browser.
            loginTimer.Stop();
            Close();
        }
        catch (SessionExpiredException) { /* A cookie alone is not proof of completed login. */ }
        catch (OperationCanceledException) { if (!IsDisposed) message.Text = "Não foi possível confirmar o login. Tente novamente."; }
        catch
        {
            lastSessionFingerprint = null;
            loginTimer.Stop();
            if (!IsDisposed) message.Text = "Não foi possível confirmar ou salvar a sessão. A conta não foi adicionada. Verifique conexão e armazenamento; clique em Recarregar para tentar novamente.";
        }
        finally { checkingLogin = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            loginTimer.Dispose();
            playTimer.Dispose();
            lifetime.Cancel();
            // Async callbacks may still be completing; the token source is kept
            // alive until the form becomes unreachable to avoid disposal races.
        }
        base.Dispose(disposing);
    }
}
