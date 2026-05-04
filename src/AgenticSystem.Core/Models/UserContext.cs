namespace AgenticSystem.Core.Models;

/// <summary>
/// Contexto do usuário para personalização de respostas
/// </summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = Tenant.DefaultTenantId;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public string Language { get; set; } = "pt-BR";
    
    /// <summary>
    /// Preferências do usuário
    /// </summary>
    public Dictionary<string, object> Preferences { get; set; } = new();
    
    /// <summary>
    /// Atividades recentes para contexto
    /// </summary>
    public List<string> RecentActivities { get; set; } = new();
    
    /// <summary>
    /// Domínios de interesse/expertise
    /// </summary>
    public List<string> Domains { get; set; } = new();
    
    /// <summary>
    /// Configurações de workspace (Obsidian, etc.)
    /// </summary>
    public WorkspaceConfig? Workspace { get; set; }
}

/// <summary>
/// Configuração do workspace do usuário
/// </summary>
public class WorkspaceConfig
{
    public string ObsidianVaultPath { get; set; } = string.Empty;
    public string DefaultNotesPath { get; set; } = string.Empty;
    public List<string> ProjectPaths { get; set; } = new();
    public Dictionary<string, string> CustomMappings { get; set; } = new();
}