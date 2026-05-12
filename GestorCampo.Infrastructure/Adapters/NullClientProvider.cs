using GestorCampo.Domain.Interfaces.Providers;
using GestorCampo.Domain.Models;

namespace GestorCampo.Infrastructure.Adapters;

public class NullClientProvider : IClientProvider
{
    public Task<List<ExternalClientData>> FetchAsync(CancellationToken ct = default) =>
        Task.FromResult(new List<ExternalClientData>());
}
