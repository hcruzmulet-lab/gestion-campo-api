using GestorCampo.Domain.Interfaces.Providers;
using GestorCampo.Domain.Models;

namespace GestorCampo.Infrastructure.Adapters;

public class NullProductProvider : IProductProvider
{
    public Task<List<ExternalProductData>> FetchAsync(CancellationToken ct = default) =>
        Task.FromResult(new List<ExternalProductData>());
}
