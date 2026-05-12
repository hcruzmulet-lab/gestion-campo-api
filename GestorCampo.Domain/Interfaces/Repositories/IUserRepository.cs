using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default);
    Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<(List<User> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        UserRole? role, bool? isActive, string? search,
        Guid? supervisorFilter, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
