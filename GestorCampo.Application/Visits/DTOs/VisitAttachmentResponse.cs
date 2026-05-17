namespace GestorCampo.Application.Visits.DTOs;

public class VisitAttachmentResponse
{
    public Guid Id { get; set; }
    public Guid VisitId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime CreatedAt { get; set; }
}
