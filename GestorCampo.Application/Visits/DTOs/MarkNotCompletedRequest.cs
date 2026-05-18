using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Visits.DTOs;

public class MarkNotCompletedRequest
{
    public VisitNotCompletedReason Reason { get; set; }
    public string? ReasonNote { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}
