using System.Text.RegularExpressions;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Security;

public sealed record FidesBuiltInRule(string Name, Regex Pattern, string Mask);

public static class FidesBuiltInRules
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    public static IReadOnlyList<FidesBuiltInRule> All { get; } =
    [
        new(FidesDetectorCatalog.Cpf,
            new Regex(@"\b(?:\d{3}\.\d{3}\.\d{3}-\d{2}|\d{11})\b", RegexOptions.Compiled, MatchTimeout),
            "[CPF MASCARADO]"),
        new(FidesDetectorCatalog.CreditCard,
            new Regex(@"\b(?:\d{4}[ -]?){3}\d{4}\b", RegexOptions.Compiled, MatchTimeout),
            "[CARTÃO MASCARADO]"),
        new(FidesDetectorCatalog.Email,
            new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout),
            "[EMAIL MASCARADO]"),
        new(FidesDetectorCatalog.CredentialToken,
            new Regex(@"\b(?:sk-[a-zA-Z0-9]{24,}|eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+|github_pat_[a-zA-Z0-9_]{20,}|gh[pousr]_[a-zA-Z0-9]{20,}|xox[baprs]-[a-zA-Z0-9-]{10,}|AKIA[0-9A-Z]{16})\b", RegexOptions.Compiled, MatchTimeout),
            "[TOKEN MASCARADO]")
    ];

    public static bool IsEnabled(string name, FidesTenantPolicy policy) =>
        FidesDetectorCatalog.Mandatory.Contains(name) ||
        !policy.EnabledDetectors.TryGetValue(name, out var enabled) || enabled;
}
