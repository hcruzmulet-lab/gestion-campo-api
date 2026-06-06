namespace GestorCampo.Application.Orders.DTOs;

public class BulkDeleteOrdersRequest
{
    public List<Guid> Ids { get; set; } = new();
}

public record BulkDeleteFailure(Guid Id, string Error);

public record BulkDeleteResult(IReadOnlyCollection<Guid> Deleted, IReadOnlyCollection<BulkDeleteFailure> Failed);
