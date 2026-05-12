using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Visits.DTOs;

public class CheckOutRequest
{
    public VisitStatus FinalStatus { get; set; } // Completed or NotCompleted
    public string? Notes { get; set; }
    public string? Result { get; set; }
    public string? Comment { get; set; } // Required if FinalStatus == NotCompleted
}
