using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Client?> GetByExternalIdAsync(string externalId, CancellationToken ct = default);
    Task<Client?> GetByTaxIdAsync(string taxId, CancellationToken ct = default);
    Task<bool> TaxIdExistsAsync(string taxId, CancellationToken ct = default);
    Task<(List<Client> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        string? search, bool? isActive, string? category,
        Guid? assignedVendorId,
        Guid? supervisorOfVendor,
        CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
}
