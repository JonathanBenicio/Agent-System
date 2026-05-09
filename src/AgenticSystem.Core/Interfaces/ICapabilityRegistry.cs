using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Registry for agent/skill capabilities with matching and composition.
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>
    /// Registers a capability declaration.
    /// </summary>
    Task RegisterCapabilityAsync(
        CapabilityDeclaration capability,
        CancellationToken ct = default);

    /// <summary>
    /// Finds capabilities that match a given request description.
    /// </summary>
    Task<CapabilityMatchResult> MatchCapabilitiesAsync(
        string requestDescription,
        List<string>? requiredInputTypes = null,
        List<string>? requiredOutputTypes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all capabilities for a given owner (agent/skill).
    /// </summary>
    Task<IReadOnlyList<CapabilityDeclaration>> GetCapabilitiesForOwnerAsync(
        string ownerId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all registered capabilities, optionally filtered by category.
    /// </summary>
    Task<IReadOnlyList<CapabilityDeclaration>> ListCapabilitiesAsync(
        string? category = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a composable skill from a chain of capabilities.
    /// </summary>
    Task<ComposableSkill> ComposeSkillAsync(
        string name,
        List<string> capabilityIds,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a capability registration.
    /// </summary>
    Task RemoveCapabilityAsync(string capabilityId, CancellationToken ct = default);
}
