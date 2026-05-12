using GestorCampo.Domain.Interfaces.Providers;

namespace GestorCampo.Infrastructure.Adapters;

public class NullOrderExporter : IOrderExporter
{
    public Task<bool> ExportAsync(Guid orderId, CancellationToken ct = default) =>
        Task.FromResult(true);
}
