using GestorCampo.Domain.Common;

namespace GestorCampo.Domain.Entities;

public class VisitAttachment : BaseEntity
{
    public Guid VisitId { get; set; }
    public Visit Visit { get; set; } = null!;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
