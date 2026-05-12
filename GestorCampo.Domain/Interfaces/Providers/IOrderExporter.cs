namespace GestorCampo.Domain.Interfaces.Providers;

public interface IOrderExporter
{
    Task<bool> ExportAsync(Guid orderId, CancellationToken ct = default);
}
