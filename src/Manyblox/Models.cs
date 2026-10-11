using System.Globalization;
using System.Text.RegularExpressions;

namespace Manyblox;

public sealed record AccountEntry(Guid Id, string Username, string Alias, string Category, long UserId);
public sealed record LocalState(int Version, List<AccountEntry> Accounts, Guid? SelectedId, long LastPlaceId)
{
    public static LocalState Empty() => new(1, [], null, 0);
}

public static partial class Rules
{
    [GeneratedRegex(@"\A[A-Za-z0-9_]{3,20}\z", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    public static void Validate(LocalState state)
    {
        if (state.Version != 1 || state.Accounts is null || state.Accounts.Count > 2000 || state.LastPlaceId < 0)
            throw new InvalidDataException("Formato local inválido.");
        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in state.Accounts)
        {
            if (a is null || a.Id == Guid.Empty || !ids.Add(a.Id) || a.Username is null ||
                !UsernamePattern().IsMatch(a.Username) || !names.Add(a.Username) || a.UserId < 0 ||
                !SafeLabel(a.Alias, 80) || !SafeLabel(a.Category, 80))
                throw new InvalidDataException("Cadastro inválido ou duplicado. Não informe senhas ou cookies.");
        }
        if (state.SelectedId is Guid selected && !ids.Contains(selected))
            throw new InvalidDataException("Seleção inválida.");
    }

    private static bool SafeLabel(string? value, int limit) => value is not null && value.Length <= limit &&
        !value.Any(char.IsControl) && !value.Contains("ROBLOSECURITY", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("WARNING:-DO-NOT-SHARE", StringComparison.OrdinalIgnoreCase);

    public static long ParseId(string value, bool optional = false)
    {
        if (optional && string.IsNullOrWhiteSpace(value)) return 0;
        if (value.Length > 19 || value.Length == 0 || value.Any(c => c is < '0' or > '9') ||
            !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0)
            throw new InvalidDataException("Informe um ID numérico positivo.");
        return id;
    }
}
