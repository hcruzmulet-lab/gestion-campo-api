using GestorCampo.Domain.Models;

namespace GestorCampo.Domain.Interfaces.Providers;

public interface IProductProvider
{
    Task<List<ExternalProductData>> FetchAsync(CancellationToken ct = default);
}
