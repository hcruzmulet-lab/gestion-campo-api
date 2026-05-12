using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Visits.DTOs;

public class VisitListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public VisitStatus? Status { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? ClientId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
