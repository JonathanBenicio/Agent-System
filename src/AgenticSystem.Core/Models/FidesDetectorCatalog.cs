namespace AgenticSystem.Core.Models;

public static class FidesDetectorCatalog
{
    public const string Cpf = "Cpf";
    public const string CreditCard = "CreditCard";
    public const string Email = "Email";
    public const string CredentialToken = "CredentialToken";

    public static IReadOnlyDictionary<string, bool> Defaults { get; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [Cpf] = true,
            [CreditCard] = true,
            [Email] = true,
            [CredentialToken] = true
        };

    public static IReadOnlySet<string> Mandatory { get; } = new HashSet<string>(
        [CredentialToken],
        StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, bool> Normalize(IReadOnlyDictionary<string, bool>? overrides)
    {
        var normalized = new Dictionary<string, bool>(Defaults, StringComparer.OrdinalIgnoreCase);
        if (overrides is null)
            return normalized;

        foreach (var (name, enabled) in overrides)
        {
            if (!Defaults.ContainsKey(name))
                throw new ArgumentException($"Unknown FIDES detector '{name}'.", nameof(overrides));
            if (Mandatory.Contains(name) && !enabled)
                throw new ArgumentException($"Required FIDES detector '{name}' cannot be disabled.", nameof(overrides));
            normalized[name] = enabled;
        }

        return normalized;
    }
}
