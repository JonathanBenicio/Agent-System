namespace AgenticSystem.Core.Models;

/// <summary>
/// Hierarchy de agents baseada no Tier System do Baianinho-Labs
/// </summary>
public enum AgentTier
{
    /// <summary>
    /// Tier 0: Chief - Meta-coordinators (Tech Lead)
    /// </summary>
    Chief = 0,
    
    /// <summary>
    /// Tier 1: Master - Domain specialists
    /// </summary>
    Master = 1,
    
    /// <summary>
    /// Tier 2: Specialist - Task executors
    /// </summary>
    Specialist = 2,
    
    /// <summary>
    /// Tier 3: Support - Tool wrappers
    /// </summary>
    Support = 3
}

/// <summary>
/// Nível de complexidade da requisição
/// </summary>
public enum ComplexityLevel
{
    Simple,
    Moderate,
    Complex,
    RequiresPlanning
}

/// <summary>
/// Tipo de intenção identificada
/// </summary>
public enum IntentType
{
    Create,
    Read,
    Update,
    Delete,
    Analyze,
    Plan,
    Learn,
    Chat,
    CreateAgent,
    Delegate,
    Setup
}

/// <summary>
/// Escopo de busca na memória
/// </summary>
public enum SearchScope
{
    All,
    Notes,
    Agents,
    Decisions,
    Domain,
    Recent
}