using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token, ct);

    public Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        user.Email = user.Email.ToLowerInvariant();
        await _db.Users.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        _db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<(List<User> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        UserRole? role, bool? isActive, string? search,
        Guid? supervisorFilter, CancellationToken ct = default)
    {
        var query = _db.Users.AsQueryable();

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));
        if (supervisorFilter.HasValue)
            query = query.Where(u => u.SupervisorId == supervisorFilter.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
