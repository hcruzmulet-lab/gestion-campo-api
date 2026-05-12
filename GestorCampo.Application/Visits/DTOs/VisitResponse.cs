using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Visits.DTOs;

public class VisitResponse
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public Guid PlannedById { get; set; }
    public DateTime PlannedAt { get; set; }
    public VisitStatus Status { get; set; }
    public DateTime? CheckinAt { get; set; }
    public DateTime? CheckoutAt { get; set; }
    public string? Notes { get; set; }
    public string? Result { get; set; }
    public string? Comment { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public Guid? RelatedOrderId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
