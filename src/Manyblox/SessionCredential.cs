using System.Security.Cryptography;
using System.Text;

namespace Manyblox;

public static class SessionCredential
{
    public static bool IsValidCookie(string value) => value.Length is > 0 and <= 16384 &&
        value.All(c => c is >= '!' and <= '~' && c is not ';' and not ',' and not '"' and not '\\');

    public static bool IsCiphertext(string value)
    {
        try { return value.Length <= 32768 && Convert.FromBase64String(value).Length is > 0 and <= 24576; }
        catch (FormatException) { return false; }
    }

    public static string Protect(string session)
    {
        if (!IsValidCookie(session)) throw new InvalidDataException("Sessão inválida.");
        byte[] plain = Encoding.UTF8.GetBytes(session);
        try { return Convert.ToBase64String(UserProtection.Protect(plain)); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public static string Unprotect(string encrypted)
    {
        if (!IsCiphertext(encrypted)) throw new InvalidDataException("Sessão indisponível.");
        byte[] plain = UserProtection.Unprotect(Convert.FromBase64String(encrypted));
        try
        {
            string session = Encoding.UTF8.GetString(plain);
            if (!IsValidCookie(session)) throw new InvalidDataException("Sessão inválida.");
            return session;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
