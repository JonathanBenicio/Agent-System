namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// #35 — Prompt Injection Defense
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Result of prompt injection detection analysis.
/// </summary>
public class PromptInjectionAnalysis
{
    public string InputText { get; init; } = string.Empty;
    public bool IsSuspicious { get; init; }
    public double ThreatScore { get; init; } // 0.0 (safe) → 1.0 (definite injection)
    public List<InjectionIndicator> Indicators { get; init; } = [];
    public string? SanitizedText { get; init; }
    public InjectionAction RecommendedAction { get; init; }
}

public class InjectionIndicator
{
    public string Pattern { get; init; } = string.Empty;
    public InjectionType Type { get; init; }
    public int Position { get; init; }
    public double Confidence { get; init; }
}

public enum InjectionType
{
    RoleOverride,          // "Ignore previous instructions..."
    SystemPromptLeak,      // "Print your system prompt"
    InstructionInjection,  // Hidden instructions in user content
    EncodingAttack,        // Base64/Unicode obfuscation
    ContextManipulation,   // Attempts to redefine context boundaries
    JailbreakAttempt       // Known jailbreak patterns
}

public enum InjectionAction
{
    Allow,      // No threat detected
    Sanitize,   // Remove suspicious content
    Block,      // Block the request entirely
    Flag,       // Allow but flag for review
    Quarantine  // Isolate and escalate
}

// ═══════════════════════════════════════════════════════════
// #36 — Data Loss Prevention (DLP)
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Result of DLP scanning on text content.
/// </summary>
public class DlpScanResult
{
    public string ContentHash { get; init; } = string.Empty;
    public bool ContainsSensitiveData { get; init; }
    public List<PiiDetection> Detections { get; init; } = [];
    public string? RedactedText { get; init; }
    public DlpAction ActionTaken { get; init; }
}

/// <summary>
/// A single PII detection within text.
/// </summary>
public class PiiDetection
{
    public PiiType Type { get; init; }
    public int StartIndex { get; init; }
    public int Length { get; init; }
    public string OriginalValue { get; init; } = string.Empty;
    public string MaskedValue { get; init; } = string.Empty;
    public double Confidence { get; init; }
}

public enum PiiType
{
    Email,
    Phone,
    CPF,
    CNPJ,
    CreditCard,
    SocialSecurity,
    Address,
    FullName,
    DateOfBirth,
    IpAddress,
    ApiKey,
    Password,
    BankAccount,
    Custom
}

public enum DlpAction
{
    None,
    Redact,     // Replace PII with [REDACTED]
    Mask,       // Partially mask (e.g., ***@email.com)
    Tokenize,   // Replace with reversible tokens
    Block,      // Prevent processing
    Audit       // Log detection, allow processing
}
