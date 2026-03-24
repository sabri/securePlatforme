using IntelliLog.Domain.Enums;

namespace IntelliLog.Domain.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
