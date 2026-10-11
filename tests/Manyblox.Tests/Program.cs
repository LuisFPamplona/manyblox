using Manyblox;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

if (args.FirstOrDefault() == "--named-object-probe")
{
    try
    {
        if (args.Length != 3 || args.Skip(1).Any(n => !n.StartsWith("ManybloxTest_", StringComparison.Ordinal))) throw new Exception();
        foreach (var name in args.Skip(1))
        {
            using var mutex = Mutex.OpenExisting(name);
            if (mutex.WaitOne(0)) { mutex.ReleaseMutex(); throw new Exception(); }
            bool collision = false;
            try { using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, name); }
            catch (WaitHandleCannotBeOpenedException) { collision = true; }
            if (!collision) throw new Exception();
        }
        Console.WriteLine("PASS cross-process named-object probe");
    }
    catch { Environment.ExitCode = 1; }
    return;
}

int passed = 0;
void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); passed++; }
void Check(bool condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0) { if (!condition) throw new Exception($"Assertion failed at test line {line}"); }
void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-data", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var account = new AccountEntry(Guid.NewGuid(), "Test_User", "Principal", "Pessoal", 123);
    var state = new LocalState(1, [account], account.Id, 456);

    Test("separate Windows process confirms mutex ownership and wrong-type event collision", () =>
    {
        var names = new[] { "ManybloxTest_" + Guid.NewGuid().ToString("N"), "ManybloxTest_" + Guid.NewGuid().ToString("N") };
        using var holder = new NamedObjectReservation(names);
        holder.Ready.GetAwaiter().GetResult();
        var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if ((Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "").Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--named-object-probe");
        foreach (var name in names) start.ArgumentList.Add(name);
        using var probe = System.Diagnostics.Process.Start(start)!;
        Check(probe.WaitForExit(10000));
        Check(probe.ExitCode == 0 && probe.StandardOutput.ReadToEnd().Contains("PASS cross-process named-object probe"));
    });

    Test("named reservations hold mutexes on their owner thread and block event type creation", () =>
    {
        var names = new[] { "ManybloxTest_" + Guid.NewGuid().ToString("N"), "ManybloxTest_" + Guid.NewGuid().ToString("N") };
        using (var holder = new NamedObjectReservation(names))
        {
            holder.Ready.GetAwaiter().GetResult();
            using var probe = Mutex.OpenExisting(names[0]);
            Check(!probe.WaitOne(0)); // Test thread must not obtain ownership.
            Reject(() => { using var conflicting = new EventWaitHandle(false, EventResetMode.AutoReset, names[1]); });
        }
        Check(!Mutex.TryOpenExisting(names[1], out var remaining));
        remaining?.Dispose();
        using var eventAfterRelease = new EventWaitHandle(false, EventResetMode.AutoReset, names[1], out bool created);
        Check(created);
    });
    Test("occupied object refuses activation and rolls back newly created resources", () =>
    {
        var names = new[] { "ManybloxTest_" + Guid.NewGuid().ToString("N"), "ManybloxTest_" + Guid.NewGuid().ToString("N") };
        using var collision = new EventWaitHandle(false, EventResetMode.AutoReset, names[1]);
        using (var holder = new NamedObjectReservation(names)) Reject(() => holder.Ready.GetAwaiter().GetResult());
        Check(!Mutex.TryOpenExisting(names[0], out var remaining));
        remaining?.Dispose();
        Check(!collision.WaitOne(0)); // Existing event was not signaled or destroyed.
        using var occupiedMutex = new Mutex(false, names[0]);
        using var secondHolder = new NamedObjectReservation(names);
        Reject(() => secondHolder.Ready.GetAwaiter().GetResult());
    });
    Test("activation before clients, disable guard and disposal release only local test objects", () =>
    {
        IReadOnlyList<ClientProcess> clients = [];
        var names = new[] { "ManybloxTest_" + Guid.NewGuid().ToString("N"), "ManybloxTest_" + Guid.NewGuid().ToString("N") };
        using (var manager = new MultiInstanceManager(() => clients, launchersActive: () => false, createReservation: () => new NamedObjectReservation(names)))
        {
            clients = [new(1234, DateTime.UtcNow, "synthetic")];
            Reject(() => manager.EnableAsync().GetAwaiter().GetResult());
            Check(!manager.Enabled);
            clients = [];
            manager.EnableAsync().GetAwaiter().GetResult();
            Check(manager.Enabled);
            clients = [new(1234, DateTime.UtcNow, "synthetic")];
            Reject(manager.Disable);
            Check(manager.Enabled);
        }
        Check(!Mutex.TryOpenExisting(names[1], out var remaining));
        remaining?.Dispose();
    });
    Test("handoff alone is not success; stable candidate and manual identity are distinct", () =>
    {
        IReadOnlyList<ClientProcess> clients = [];
        var now = DateTime.UtcNow;
        using var manager = new MultiInstanceManager(() => clients, () => now);
        var launch = manager.BeginLaunch("Test_User");
        manager.BeforeHandoff(launch);
        manager.Poll();
        Check(launch.Phase == LaunchPhase.AwaitingProcess && launch.Client is null && !launch.AccountConfirmedByUser);
        Reject(() => manager.BeginLaunch("Other_User"));
        clients = [new(3210, now, "synthetic")];
        manager.Poll();
        Check(launch.Phase == LaunchPhase.ProcessObserved && !launch.AccountConfirmedByUser);
        Reject(() => manager.ConfirmAccountByUser(launch));
        now = now.AddSeconds(11);
        manager.Poll();
        Check(launch.Phase == LaunchPhase.StableProcess && !launch.AccountConfirmedByUser);
        manager.ConfirmAccountByUser(launch);
        Check(launch.AccountConfirmedByUser);
        clients = [];
        manager.Poll();
        Check(launch.Phase == LaunchPhase.Ended && !launch.AccountConfirmedByUser);
    });
    Test("replacement, ambiguity, timeout and reused PID are not authenticated success", () =>
    {
        var now = DateTime.UtcNow;
        var original = new ClientProcess(4010, now, "synthetic");
        IReadOnlyList<ClientProcess> clients = [];
        var names = new[] { "ManybloxTest_" + Guid.NewGuid().ToString("N"), "ManybloxTest_" + Guid.NewGuid().ToString("N") };
        using var manager = new MultiInstanceManager(() => clients, () => now, launchersActive: () => false,
            createReservation: () => new NamedObjectReservation(names));
        manager.EnableAsync().GetAwaiter().GetResult();
        clients = [original];
        var launch = manager.BeginLaunch("Test_User");
        manager.BeforeHandoff(launch);
        clients = [new(4011, now.AddSeconds(1), "synthetic")];
        manager.Poll();
        Check(launch.Code == "PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT");
        clients = [];
        manager.Poll();
        var ambiguous = manager.BeginLaunch("Other_User");
        manager.BeforeHandoff(ambiguous);
        clients = [new(4012, now, "synthetic"), new(4013, now, "synthetic")];
        manager.Poll();
        Check(ambiguous.Phase == LaunchPhase.Ambiguous && ambiguous.Client is null);
        var timeout = manager.BeginLaunch("Third_User");
        manager.BeforeHandoff(timeout);
        now = now.AddSeconds(91);
        manager.Poll();
        Check(timeout.Phase == LaunchPhase.TimedOut);
        var reused = manager.BeginLaunch("Fourth_User");
        manager.BeforeHandoff(reused);
        clients = [new(4012, now, "synthetic")];
        manager.Poll();
        Check(reused.Client?.Started == now && !reused.AccountConfirmedByUser);
    });
    Test("disabled multi blocks a second launch and a client appearing before handoff", () =>
    {
        IReadOnlyList<ClientProcess> clients = [];
        var diagnostics = new List<InstanceDiagnostic>();
        using var manager = new MultiInstanceManager(() => clients, diagnostic: diagnostics.Add);
        var first = manager.BeginLaunch("Test_User");
        clients = [new(5100, DateTime.UtcNow, "synthetic")];
        Reject(() => manager.BeforeHandoff(first));
        Check(first.Phase == LaunchPhase.PreparingBrowser);
        manager.BrowserClosed(first);
        Reject(() => manager.BeginLaunch("Other_User"));
        Check(manager.Launches.Count == 1);
        Check(diagnostics.Any(d => d.Code == "SECOND_LAUNCH_BLOCKED_MULTI_DISABLED" && !d.MultiEnabled && !d.ObjectsHeld));
        Check(diagnostics.Any(d => d.Code == "CLIENT_BASELINE" && d.Pid == 5100 && d.StartedUtc == clients[0].Started));
        Check(!diagnostics.Any(d => d.Code == "PROTOCOL_REQUESTED"));
    });
    Test("prepared handoff diagnostics correlate baseline, replacement and PID reuse", () =>
    {
        IReadOnlyList<ClientProcess> clients = [];
        var now = DateTime.UtcNow;
        var diagnostics = new List<InstanceDiagnostic>();
        var names = new[] { "ManybloxTest_" + Guid.NewGuid().ToString("N"), "ManybloxTest_" + Guid.NewGuid().ToString("N") };
        using var manager = new MultiInstanceManager(() => clients, () => now, launchersActive: () => false,
            createReservation: () => new NamedObjectReservation(names), diagnostic: diagnostics.Add);
        manager.EnableAsync().GetAwaiter().GetResult();
        var original = new ClientProcess(5200, now, "synthetic");
        clients = [original];
        var launch = manager.BeginLaunch("Test_User");
        manager.BeforeHandoff(launch);
        Check(diagnostics.Any(d => d.Code == "CLIENT_BASELINE" && d.LaunchId == launch.Id && d.StartedUtc == original.Started && d.MultiEnabled && d.ObjectsHeld));
        Check(diagnostics.Any(d => d.Code == "PROTOCOL_REQUESTED" && d.LaunchId == launch.Id && d.ClientCount == 1));
        clients = [new(5200, now.AddSeconds(1), "synthetic")];
        manager.Poll();
        Check(diagnostics.Any(d => d.Code == "CLIENT_DISAPPEARED" && d.Pid == original.Pid && d.StartedUtc == original.Started));
        Check(diagnostics.Any(d => d.Code == "CLIENT_APPEARED" && d.Pid == original.Pid && d.StartedUtc != original.Started));
        Check(launch.Code == "PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT" && !launch.AccountConfirmedByUser);
        var logger = new TechnicalLog(root);
        logger.WriteDiagnostic(diagnostics.Single(d => d.Code == "PROTOCOL_REQUESTED"));
        string line = File.ReadLines(Path.Combine(root, "multi-instance.jsonl")).Last();
        using var json = System.Text.Json.JsonDocument.Parse(line);
        Check(json.RootElement.GetProperty("multiEnabled").GetBoolean());
        Check(json.RootElement.GetProperty("objectsHeld").GetBoolean());
        Check(json.RootElement.GetProperty("launchId").GetGuid() == launch.Id);
        Check(!line.Contains("Test_User") && !line.Contains("synthetic"));
    });
    Test("browser cancellation, monitoring failure and log secrets fail closed", () =>
    {
        bool fail = false;
        using var manager = new MultiInstanceManager(() => fail ? throw new IOException("synthetic-private-text") : []);
        var launch = manager.BeginLaunch("Test_User");
        manager.BrowserClosed(launch);
        Check(launch.Phase == LaunchPhase.Failed);
        fail = true;
        manager.Poll();
        Check(!manager.MonitoringAvailable);
        Reject(() => manager.Preflight());
        var logger = new TechnicalLog(root);
        logger.Write(".ROBLOSECURITY=DO_NOT_LOG", 1);
        logger.Write("SYNTHETICSECRET", 1);
        logger.Write("PROCESS_SNAPSHOT_FAILED", 1);
        string logText = File.ReadAllText(Path.Combine(root, "multi-instance.jsonl"));
        Check(logText.Contains("PROCESS_SNAPSHOT_FAILED") && !logText.Contains("ROBLOSECURITY") && !logText.Contains("SYNTHETICSECRET"));
    });
    Test("numeric ID injection rejected", () => { foreach (var id in new[] { "1&x=evil", "-1", "0", "1+launch", "https://evil", "9223372036854775808", " 1", "1\n" }) Reject(() => Rules.ParseId(id)); });
    Test("duplicate accounts and invalid state rejected", () => { Reject(() => Rules.Validate(state with { Accounts = [account, account] })); Reject(() => Rules.Validate(state with { Version = 2 })); Reject(() => Rules.Validate(state with { SelectedId = Guid.NewGuid() })); });
    Test("credential marker rejected", () => Reject(() => Rules.Validate(state with { Accounts = [account with { Alias = ".ROBLOSECURITY" }] })));
    Test("URI policy", () => { Check(RobloxActions.LaunchUri(456).AbsoluteUri == "roblox://experiences/start?placeId=456"); Check(RobloxActions.GameUri(456).Host == "www.roblox.com"); Reject(() => RobloxActions.LaunchUri(-1)); foreach (var url in new[] { "https://evil.example", "file:///C:/Windows/System32/cmd.exe", "https://www.roblox.com/games/1?cookie=secret", "https://user@www.roblox.com/users/1/profile", "roblox://experiences/start?placeId=1&command=evil" }) Check(!RobloxActions.IsAllowed(new Uri(url))); });
    Test("overlapping URI prefix and suffix rejected", () => Check(!RobloxActions.IsAllowed(new Uri("https://www.roblox.com/users/profile"))));
    Test("internal browser official origins only", () =>
    {
        Check(BrowserPolicy.IsOfficialPage("https://www.roblox.com/login"));
        Check(BrowserPolicy.IsOfficialPage("https://roblox.com/home"));
        foreach (var uri in new[] { "http://www.roblox.com/login", "https://www.roblox.com.evil.example/login", "https://evilroblox.com/", "https://user@www.roblox.com/login", "https://www.roblox.com:444/login", "file:///C:/Windows", "javascript:alert(1)", "data:text/html,test", "https://apis.roblox.com/", "about:blank" })
            Check(!BrowserPolicy.IsOfficialPage(uri));
    });
    Test("client launch requires official initiator and user gesture", () =>
    {
        const string source = "https://www.roblox.com/games/456";
        Check(BrowserPolicy.CanLaunchClient("roblox-player:synthetic", "https://www.roblox.com", source, true));
        Check(!BrowserPolicy.CanLaunchClient("roblox-player:synthetic", "https://www.roblox.com", source, false));
        Check(!BrowserPolicy.CanLaunchClient("roblox-player:synthetic", "https://evil.example", source, true));
        Check(!BrowserPolicy.CanLaunchClient("roblox-player:synthetic", "https://www.roblox.com", "https://evil.example", true));
        foreach (var uri in new[] { "cmd:test", "powershell:test", "file:///C:/Windows", "ms-settings:test", "https://evil.example" })
            Check(!BrowserPolicy.CanLaunchClient(uri, "https://www.roblox.com", source, true));
    });
    Test("session credentials encrypted separately and input injection rejected", () =>
    {
        const string synthetic = "SYNTHETIC_SESSION_NOT_A_REAL_CREDENTIAL";
        string encrypted = SessionCredential.Protect(synthetic);
        Check(!encrypted.Contains(synthetic) && SessionCredential.Unprotect(encrypted) == synthetic);
        foreach (var value in new[] { "", "x\r\nInjected:1", "x;y=z", "x,y", "x\"y", "x\\y", new string('a', 16385) }) Reject(() => SessionCredential.Protect(value));
        Reject(() => SessionCredential.Unprotect("not-base64"));
        var bytes = Convert.FromBase64String(encrypted); bytes[bytes.Length / 2] ^= 0xff;
        Reject(() => SessionCredential.Unprotect(Convert.ToBase64String(bytes)));
    });
    Test("session persisted through restart without plaintext credentials", () =>
    {
        const string synthetic = "SYNTHETIC_SESSION_FOR_STORAGE_TEST";
        string sessionPath = Path.Combine(root, "sessions");
        var connected = account with { ProtectedSession = SessionCredential.Protect(synthetic) };
        using (var store = new StateStore(sessionPath))
        {
            store.Save(state with { Accounts = [connected] });
            byte[] disk = File.ReadAllBytes(store.DataPath);
            Check(!Encoding.UTF8.GetString(disk).Contains(synthetic));
            Check(!Encoding.UTF8.GetString(UserProtection.Unprotect(disk)).Contains(synthetic));
        }
        using var reopened = new StateStore(sessionPath);
        Check(SessionCredential.Unprotect(reopened.Load().Accounts.Single().ProtectedSession) == synthetic);
    });
    Test("oversized session database cannot replace valid saved data", () =>
    {
        using var store = new StateStore(Path.Combine(root, "capacity"));
        store.Save(state);
        string ciphertext = SessionCredential.Protect(new string('a', 16384));
        var entries = Enumerable.Range(1, 200).Select(i => new AccountEntry(Guid.NewGuid(), "User_" + i, "", "", i, ciphertext)).ToList();
        Reject(() => store.Save(new LocalState(1, entries, null, 0)));
        Check(store.Load().Accounts.Single() == account);
    });
    Test("authenticated identity uses fixed official destination", () =>
    {
        int requests = 0;
        using var api = new RobloxSessionApi(new StubHttpHandler(request =>
        {
            requests++;
            Check(request.RequestUri!.AbsoluteUri == "https://users.roblox.com/v1/users/authenticated");
            Check(request.Method == HttpMethod.Get && request.RequestUri.Query == "");
            Check(request.Headers.GetValues("Cookie").Single() == ".ROBLOSECURITY=SYNTHETIC");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"id\":123,\"name\":\"Test_User\"}") };
        }));
        Check(api.IdentifyAsync("SYNTHETIC").GetAwaiter().GetResult() == new SessionIdentity(123, "Test_User"));
        Reject(() => api.IdentifyAsync("x\r\nInjected:1").GetAwaiter().GetResult());
        Check(requests == 1);
    });
    Test("expired sessions and mismatched account identity rejected", () =>
    {
        using var expired = new RobloxSessionApi(new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)));
        Reject(() => expired.IdentifyAsync("SYNTHETIC").GetAwaiter().GetResult());
        using var different = new RobloxSessionApi(new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"id\":456,\"name\":\"Different_User\"}") }));
        Reject(() => different.ValidateAccountAsync(account with { ProtectedSession = SessionCredential.Protect("SYNTHETIC") }).GetAwaiter().GetResult());
    });
    Test("redirects and oversized identity responses rejected", () =>
    {
        int requests = 0;
        using var redirect = new RobloxSessionApi(new StubHttpHandler(_ =>
        {
            requests++;
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://evil.example/");
            return response;
        }));
        Reject(() => redirect.IdentifyAsync("SYNTHETIC").GetAwaiter().GetResult());
        Check(requests == 1);
        using var oversized = new RobloxSessionApi(new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(new string('x', 65537)) }));
        Reject(() => oversized.IdentifyAsync("SYNTHETIC").GetAwaiter().GetResult());
    });
    Test("PLAY bound to selected game and protocol forwarding rejects unsafe data", () =>
    {
        Check(BrowserPolicy.IsGamePage("https://www.roblox.com/games/456/Game", 456));
        Check(!BrowserPolicy.IsGamePage("https://www.roblox.com/games/4560", 456));
        Check(!BrowserPolicy.IsGamePage("https://www.roblox.com/home", 456));
        Check(!BrowserPolicy.IsGamePage("https://evil.example/games/456", 456));
        foreach (var uri in new[] { "cmd:test", "roblox-player:test\r\nunsafe", "roblox-player:test\"unsafe", new string('x', 16385) })
            Reject(() => RobloxActions.ForwardOfficialClientRequest(uri, "https://www.roblox.com", "https://www.roblox.com/games/456", true));
    });
    Test("DPAPI round trip and tamper rejection", () => { byte[] data = Encoding.UTF8.GetBytes("synthetic-test-data"); byte[] encrypted = UserProtection.Protect(data); Check(!encrypted.SequenceEqual(data)); Check(UserProtection.Unprotect(encrypted).SequenceEqual(data)); encrypted[encrypted.Length / 2] ^= 0xff; Reject(() => UserProtection.Unprotect(encrypted)); CryptographicOperations.ZeroMemory(data); });
    string path = Path.Combine(root, "store");
    Test("save, atomic replace, restart and encrypted disk", () =>
    {
        using (var store = new StateStore(path))
        {
            Check(store.Load().Accounts.Count == 0);
            store.Save(state);
            store.Save(state with { LastPlaceId = 789 });
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(store.DataPath)).Contains("Test_User"));
            Check(Directory.GetFiles(path, "*.tmp").Length == 0);
            Reject(() => new StateStore(path));
        }
        using var reopened = new StateStore(path);
        var loaded = reopened.Load();
        Check(loaded.Accounts.Single() == account && loaded.LastPlaceId == 789 && loaded.SelectedId == account.Id);
    });
    Test("DACL excludes other ordinary users", () =>
    {
        var acl = new DirectoryInfo(path).GetAccessControl();
        Check(acl.AreAccessRulesProtected);
        var user = WindowsIdentity.GetCurrent().User!;
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            Check(rule.IdentityReference.Equals(user) || rule.IdentityReference.Equals(system));
    });
    Test("corrupt storage fails closed", () =>
    {
        using var store = new StateStore(path);
        File.WriteAllBytes(store.DataPath, [1, 2, 3]);
        Reject(() => store.Load());
        Check(File.ReadAllBytes(store.DataPath).SequenceEqual(new byte[] { 1, 2, 3 }));
    });
    Test("unknown fields and legacy credential imports rejected", () =>
    {
        using var store = new StateStore(path);
        File.WriteAllBytes(store.DataPath, UserProtection.Protect(Encoding.UTF8.GetBytes("{\"Version\":1,\"Accounts\":[],\"SelectedId\":null,\"LastPlaceId\":0,\"Cookie\":\"synthetic\"}")));
        Reject(() => store.Load());
    });
    Test("oversized state rejected before decryption", () =>
    {
        using var store = new StateStore(path);
        using (var file = File.Create(store.DataPath)) file.SetLength(4 * 1024 * 1024 + 1);
        Reject(() => store.Load());
    });
    Test("WinForms construction and rendering with synthetic data", () =>
    {
        Exception? error = null;
        var ui = new Thread(() =>
        {
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                using var store = new StateStore(Path.Combine(root, "ui"));
                var display = state with { Accounts = [account with { ProtectedSession = SessionCredential.Protect("SYNTHETIC_UI_SESSION") }, new AccountEntry(Guid.NewGuid(), "Example_Alt", "Secundária", "Jogos", 321)] };
                using var form = new MainForm(store, display);
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(Path.Combine(Environment.CurrentDirectory, "artifacts", "preview.png"));
                Check(form.Controls.Count > 0);
                var split = form.Controls.OfType<System.Windows.Forms.TabControl>().Single().TabPages[0].Controls.OfType<System.Windows.Forms.SplitContainer>().Single();
                Check(split.Panel2.Width >= 400 && split.Panel1.Width >= 300);
            }
            catch (Exception e) { error = e; }
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        ui.Join();
        if (error is not null) throw error;
    });
    if (args.Contains("--webview-smoke")) Test("official login page loads in private isolated WebView2", () =>
    {
        Exception? error = null;
        var ui = new Thread(() =>
        {
            try
            {
                string smokeRoot = Path.Combine(Environment.CurrentDirectory, "artifacts", "browser-smoke", Guid.NewGuid().ToString("N"));
                using var store = new StateStore(smokeRoot);
                using var form = new MainForm(store, LocalState.Empty());
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control parent)
                {
                    foreach (System.Windows.Forms.Control child in parent.Controls)
                    {
                        yield return child;
                        foreach (var nested in Descendants(child)) yield return nested;
                    }
                }
                var enter = Descendants(form).OfType<System.Windows.Forms.Button>().Single(b => b.Text == "+ Adicionar conta");
                Check(enter.Enabled);
                enter.PerformClick();
                var window = form.OwnedForms.Single();
                var web = Descendants(window).OfType<WebView2>().Single();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(45) &&
                    (web.CoreWebView2 is null || !BrowserPolicy.IsOfficialPage(web.CoreWebView2.Source) || string.IsNullOrWhiteSpace(web.CoreWebView2.DocumentTitle)))
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(25);
                }
                Check(web.CoreWebView2 is not null && BrowserPolicy.IsOfficialPage(web.CoreWebView2.Source));
                Check(web.CoreWebView2!.Profile.IsInPrivateModeEnabled);
                Check(!web.CoreWebView2.Settings.IsPasswordAutosaveEnabled && !web.CoreWebView2.Settings.AreDevToolsEnabled);
                Check(!web.CoreWebView2.Settings.AreHostObjectsAllowed && !web.CoreWebView2.Settings.IsWebMessageEnabled);
                // The Roblox login is a client-rendered page. Let its scripts
                // finish before capturing; an HTML shell alone is not proof of a form.
                var renderTimer = System.Diagnostics.Stopwatch.StartNew();
                while (renderTimer.Elapsed < TimeSpan.FromSeconds(12)) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(25); }
                using var preview = File.Create(Path.Combine(Environment.CurrentDirectory, "artifacts", "login-preview.png"));
                var capture = web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, preview);
                while (!capture.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(55)) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(25); }
                Check(capture.IsCompleted);
                capture.GetAwaiter().GetResult();
                window.Close();
                Check(store.Load().Accounts.Count == 0); // No account may be added without confirmed login.
                form.Close();
            }
            catch (Exception e) { error = e; }
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        ui.Join();
        if (error is not null) throw error;
    });
    Console.WriteLine($"RESULT: {passed} tests passed.");
}

finally
{
    // Only the unique directory created by this test, underneath artifacts/test-data.
    Directory.Delete(root, true);
}

sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}
