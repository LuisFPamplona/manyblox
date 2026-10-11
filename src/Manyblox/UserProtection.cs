using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Manyblox;

// Native Windows DPAPI; system DLLs only, no bundled native libraries.
public static class UserProtection
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out Blob output);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out Blob output);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    public static byte[] Protect(byte[] input) => Transform(input, true);
    public static byte[] Unprotect(byte[] input) => Transform(input, false);

    private static byte[] Transform(byte[] bytes, bool protect)
    {
        if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024) throw new InvalidDataException("Tamanho inválido.");
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var source = new Blob { Length = bytes.Length, Data = pinned.AddrOfPinnedObject() };
        Blob result = default;
        try
        {
            // UI_FORBIDDEN only: never LOCAL_MACHINE.
            bool success = protect
                ? CryptProtectData(ref source, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out result)
                : CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out result);
            if (!success) throw new CryptographicException("Falha na proteção vinculada ao usuário do Windows.");
            var output = new byte[result.Length];
            Marshal.Copy(result.Data, output, 0, result.Length);
            return output;
        }
        finally
        {
            if (result.Data != IntPtr.Zero)
            {
                // Zero decrypted native buffer before releasing it.
                for (int i = 0; i < result.Length; i++) Marshal.WriteByte(result.Data, i, 0);
                LocalFree(result.Data);
            }
            pinned.Free();
        }
    }
}
