using System.Net;
using System.Text.Json;

namespace Manyblox;

public sealed record SessionIdentity(long Id, string Username);
public sealed class SessionExpiredException : Exception
{
    public SessionExpiredException() : base("A sessão expirou. Entre novamente no Roblox.") { }
}

public sealed class RobloxSessionApi : IDisposable
{
    private static readonly Uri IdentityUri = new("https://users.roblox.com/v1/users/authenticated");
    private readonly HttpClient client;

    public RobloxSessionApi(HttpMessageHandler? handler = null)
    {
        // A credential never follows redirects or goes through a global CookieContainer.
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<SessionIdentity> IdentifyAsync(string session, CancellationToken cancellationToken = default)
    {
        if (!SessionCredential.IsValidCookie(session)) throw new InvalidDataException("Sessão inválida.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        cancellationToken = deadline.Token; // Includes body reads after ResponseHeadersRead.
        using var request = new HttpRequestMessage(HttpMethod.Get, IdentityUri);
        request.Headers.Add("Cookie", ".ROBLOSECURITY=" + session);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new SessionExpiredException();
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Não foi possível confirmar a sessão no Roblox.");
        // Never surface response bodies or headers in error messages or diagnostics.
        if (response.Content.Headers.ContentLength > 65536) throw new InvalidDataException("Resposta inválida.");
        using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var bounded = new MemoryStream();
        byte[] buffer = new byte[4096];
        while (true)
        {
            int count = await input.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            if (bounded.Length + count > 65536) throw new InvalidDataException("Resposta inválida.");
            bounded.Write(buffer, 0, count);
        }
        using var document = JsonDocument.Parse(bounded.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
        long id = document.RootElement.GetProperty("id").GetInt64();
        string name = document.RootElement.GetProperty("name").GetString() ?? "";
        var entry = new AccountEntry(Guid.NewGuid(), name, "", "Pessoal", id);
        Rules.Validate(new LocalState(1, [entry], entry.Id, 0));
        if (id <= 0) throw new InvalidDataException("Identidade inválida.");
        return new SessionIdentity(id, name);
    }

    public async Task<SessionIdentity> ValidateAccountAsync(AccountEntry account, CancellationToken cancellationToken = default)
    {
        if (account.ProtectedSession.Length == 0) throw new SessionExpiredException();
        var identity = await IdentifyAsync(SessionCredential.Unprotect(account.ProtectedSession), cancellationToken);
        if (identity.Id != account.UserId) throw new SessionExpiredException();
        return identity;
    }

    public void Dispose() => client.Dispose();
}
