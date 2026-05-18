namespace AgenticSystem.Core.Models;

public enum KnowledgeRoomRole
{
    Reader = 0,
    Editor = 1,
    Admin = 2
}

public class KnowledgeRoomPermission
{
    public string Id { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public KnowledgeRoomRole Role { get; set; } = KnowledgeRoomRole.Reader;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}

public class KnowledgeRoom
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "bg-teal-500";
    public string Icon { get; set; } = "FolderOpen";
    public int DocumentCount { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
