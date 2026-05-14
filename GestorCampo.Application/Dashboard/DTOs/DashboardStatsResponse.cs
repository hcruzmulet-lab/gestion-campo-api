namespace GestorCampo.Application.Dashboard.DTOs;

public class DashboardStatsResponse
{
    public VisitStats Visits { get; set; } = new();
    public OrderStats Orders { get; set; } = new();
    public int ActiveVendorsToday { get; set; }
    public float ConversionRate { get; set; }
    public decimal TotalApprovedValue { get; set; }
    public float VisitCompletionRate { get; set; }
}

public class VisitStats
{
    public int Planned { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int NotCompleted { get; set; }
    public int Total => Planned + InProgress + Completed + NotCompleted;
}

public class OrderStats
{
    public int Draft { get; set; }
    public int Sent { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Delivered { get; set; }
    public int Total => Draft + Sent + Approved + Rejected + Delivered;
}
