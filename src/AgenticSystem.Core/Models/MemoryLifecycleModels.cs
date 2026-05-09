namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Memory Lifecycle — Advanced Memory Management
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Enhanced memory entry with confidence, sensitivity, and type classification.
/// </summary>
public class EnhancedMemoryEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentName { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public MemoryType MemoryType { get; init; } = MemoryType.Episodic;
    public MemorySensitivity Sensitivity { get; init; } = MemorySensitivity.Normal;
    public double Confidence { get; init; } = 1.0;
    public double Freshness { get; set; } = 1.0;
    public double DecayRate { get; init; } = 0.01;
    public int AccessCount { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}

/// <summary>
/// Classification of memory types for separation and prioritization.
/// </summary>
public enum MemoryType
{
    Episodic,     // Specific events and conversations
    Semantic,     // General knowledge and facts
    Procedural,   // How-to knowledge and workflows
    WorkingMemory // Short-term, current session context
}

/// <summary>
/// Sensitivity level for memory entries.
/// </summary>
public enum MemorySensitivity
{
    Normal,
    Internal,      // Internal use only
    Confidential,  // Restricted access
    Restricted     // Highest sensitivity, auto-encrypted
}

/// <summary>
/// Configuration for memory compaction.
/// </summary>
public class MemoryCompactionConfig
{
    public int MaxEntriesBeforeCompaction { get; init; } = 1000;
    public TimeSpan CompactionInterval { get; init; } = TimeSpan.FromHours(6);
    public double MinFreshnessThreshold { get; init; } = 0.1;
    public bool MergeRelatedEntries { get; init; } = true;
    public bool ArchiveOnCompaction { get; init; } = true;
}

/// <summary>
/// Result of a memory compaction operation.
/// </summary>
public class MemoryCompactionResult
{
    public int OriginalCount { get; init; }
    public int CompactedCount { get; init; }
    public int ArchivedCount { get; init; }
    public int MergedCount { get; init; }
    public TimeSpan Duration { get; init; }
}
