using Manyblox;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

int passed = 0;
void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); passed++; }
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-data", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var account = new AccountEntry(Guid.NewGuid(), "Test_User", "Principal", "Pessoal", 123);
    var state = new LocalState(1, [account], account.Id, 456);
    Test("numeric ID injection rejected", () => { foreach (var id in new[] { "1&x=evil", "-1", "0", "1+launch", "https://evil", "9223372036854775808", " 1", "1\n" }) Reject(() => Rules.ParseId(id)); });
    Test("duplicate accounts and invalid state rejected", () => { Reject(() => Rules.Validate(state with { Accounts = [account, account] })); Reject(() => Rules.Validate(state with { Version = 2 })); Reject(() => Rules.Validate(state with { SelectedId = Guid.NewGuid() })); });
    Test("credential marker rejected", () => Reject(() => Rules.Validate(state with { Accounts = [account with { Alias = ".ROBLOSECURITY" }] })));
    Test("URI policy", () => { Check(RobloxActions.LaunchUri(456).AbsoluteUri == "roblox://experiences/start?placeId=456"); Check(RobloxActions.GameUri(456).Host == "www.roblox.com"); Reject(() => RobloxActions.LaunchUri(-1)); foreach (var url in new[] { "https://evil.example", "file:///C:/Windows/System32/cmd.exe", "https://www.roblox.com/games/1?cookie=secret", "https://user@www.roblox.com/users/1/profile", "roblox://experiences/start?placeId=1&command=evil" }) Check(!RobloxActions.IsAllowed(new Uri(url))); });
    Test("overlapping URI prefix and suffix rejected", () => Check(!RobloxActions.IsAllowed(new Uri("https://www.roblox.com/users/profile"))));
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
                using var store = new StateStore(Path.Combine(root, "ui"));
                var display = state with { Accounts = [account, new AccountEntry(Guid.NewGuid(), "Example_Alt", "Secundária", "Jogos", 321)] };
                using var form = new MainForm(store, display);
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(Path.Combine(Environment.CurrentDirectory, "artifacts", "preview.png"));
                Check(form.Controls.Count > 0);
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
