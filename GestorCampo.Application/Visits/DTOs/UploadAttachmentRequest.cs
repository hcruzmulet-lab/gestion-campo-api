namespace GestorCampo.Application.Visits.DTOs;

public class UploadAttachmentRequest
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "image/jpeg";
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
