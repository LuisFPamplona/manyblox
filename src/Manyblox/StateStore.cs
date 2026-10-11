using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Manyblox;

public sealed class StateStore : IDisposable
{
    private readonly string directory;
    private readonly FileStream sessionLock;
    private const int MaxBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Manyblox");
    public string DataPath => Path.Combine(directory, "accounts.dpapi");

    public StateStore(string path)
    {
        directory = Path.GetFullPath(path);
        // Reject junctions/symlinks in the path before writing.
        for (DirectoryInfo? d = new(directory); d is not null; d = d.Parent)
            if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Diretório redirecionado não permitido.");
        Directory.CreateDirectory(directory);
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        var user = WindowsIdentity.GetCurrent().User ?? throw new IOException("Usuário indisponível.");
        foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(acl);
        var lockPath = Path.Combine(directory, "session.lock");
        RejectLink(lockPath);
        sessionLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private static void RejectLink(string path)
    {
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Arquivo redirecionado não permitido.");
    }

    public LocalState Load()
    {
        RejectLink(DataPath);
        if (!File.Exists(DataPath)) return LocalState.Empty();
        byte[]? plain = null;
        try
        {
            using var stream = new FileStream(DataPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0 || stream.Length > MaxBytes) throw new InvalidDataException("Arquivo inválido.");
            byte[] encrypted = new byte[checked((int)stream.Length)];
            stream.ReadExactly(encrypted);
            plain = UserProtection.Unprotect(encrypted);
            var state = JsonSerializer.Deserialize<LocalState>(plain, Options) ?? throw new InvalidDataException("Arquivo inválido.");
            Rules.Validate(state);
            return state;
        }
        finally { if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }

    public void Save(LocalState state)
    {
        Rules.Validate(state);
        RejectLink(DataPath);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            byte[] encrypted = UserProtection.Protect(plain);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(encrypted);
                stream.Flush(true);
            }
            // Atomic replacement on the same NTFS volume. No plaintext fallback or backup.
            if (File.Exists(DataPath)) File.Replace(temporary, DataPath, null);
            else File.Move(temporary, DataPath);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void Dispose() => sessionLock.Dispose();
}
