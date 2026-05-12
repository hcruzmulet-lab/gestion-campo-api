using GestorCampo.Domain.Models;

namespace GestorCampo.Domain.Interfaces.Providers;

public interface IClientProvider
{
    Task<List<ExternalClientData>> FetchAsync(CancellationToken ct = default);
}
