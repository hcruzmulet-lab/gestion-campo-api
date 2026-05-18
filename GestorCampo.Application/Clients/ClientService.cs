using GestorCampo.Application.Clients.DTOs;
using GestorCampo.Application.Common;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.Clients;

public class ClientService
{
    private readonly IClientRepository _clients;
    private readonly IUserRepository _users;

    public ClientService(IClientRepository clients, IUserRepository users)
    {
        _clients = clients;
        _users = users;
    }

    public async Task<ServiceResult<ClientResponse>> CreateAsync(
        CreateClientRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        if (await _clients.TaxIdExistsAsync(request.TaxId, ct))
            return ServiceResult<ClientResponse>.Fail("El RUC/cédula ya está registrado");

        if (request.AssignedVendorId.HasValue)
        {
            var vendor = await _users.GetByIdAsync(request.AssignedVendorId.Value, ct);
            if (vendor == null || vendor.Role != UserRole.Vendor)
                return ServiceResult<ClientResponse>.Fail("El vendedor asignado no es válido");
        }

        var client = new Client
        {
            Name = request.Name,
            TaxId = request.TaxId,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            Lat = request.Lat,
            Lng = request.Lng,
            Category = request.Category,
            AssignedVendorId = request.AssignedVendorId,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId
        };

        await _clients.AddAsync(client, ct);
        return ServiceResult<ClientResponse>.Ok(ToResponse(client));
    }

    public async Task<ServiceResult<ClientResponse>> GetByIdAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var client = await _clients.GetByIdAsync(id, ct);
        if (client == null) return ServiceResult<ClientResponse>.Fail("Cliente no encontrado");

        if (currentRole == UserRole.Vendor && client.AssignedVendorId != currentUserId)
            return ServiceResult<ClientResponse>.Fail("No tiene acceso a este cliente");

        return ServiceResult<ClientResponse>.Ok(ToResponse(client));
    }

    public async Task<ServiceResult<PagedResult<ClientResponse>>> GetListAsync(
        ClientListRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var vendorFilter = currentRole == UserRole.Vendor ? currentUserId : request.AssignedVendorId;

        var (items, totalCount) = await _clients.GetListAsync(
            request.Page, pageSize,
            request.Search, request.IsActive, request.Category,
            vendorFilter, ct);

        return ServiceResult<PagedResult<ClientResponse>>.Ok(new PagedResult<ClientResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult<ClientResponse>> UpdateAsync(
        Guid id, UpdateClientRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var client = await _clients.GetByIdAsync(id, ct);
        if (client == null) return ServiceResult<ClientResponse>.Fail("Cliente no encontrado");

        if (request.AssignedVendorId.HasValue)
        {
            var vendor = await _users.GetByIdAsync(request.AssignedVendorId.Value, ct);
            if (vendor == null || vendor.Role != UserRole.Vendor)
                return ServiceResult<ClientResponse>.Fail("El vendedor asignado no es válido");
        }

        if (request.Name != null) client.Name = request.Name;
        if (request.Address != null) client.Address = request.Address;
        if (request.Phone != null) client.Phone = request.Phone;
        if (request.Email != null) client.Email = request.Email;
        if (request.Lat.HasValue) client.Lat = request.Lat;
        if (request.Lng.HasValue) client.Lng = request.Lng;
        if (request.Category != null) client.Category = request.Category;
        if (request.AssignedVendorId.HasValue) client.AssignedVendorId = request.AssignedVendorId;
        client.UpdatedBy = currentUserId;

        await _clients.UpdateAsync(client, ct);
        return ServiceResult<ClientResponse>.Ok(ToResponse(client));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        var client = await _clients.GetByIdAsync(id, ct);
        if (client == null) return ServiceResult.Fail("Cliente no encontrado");

        client.DeletedAt = DateTime.UtcNow;
        client.DeletedBy = currentUserId;
        client.IsActive = false;
        client.UpdatedBy = currentUserId;
        await _clients.UpdateAsync(client, ct);
        return ServiceResult.Ok();
    }

    private static ClientResponse ToResponse(Client c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        TaxId = c.TaxId,
        Address = c.Address,
        Phone = c.Phone,
        Email = c.Email,
        IsActive = c.IsActive,
        Lat = c.Lat,
        Lng = c.Lng,
        Category = c.Category,
        AssignedVendorId = c.AssignedVendorId,
        AssignedVendorName = c.AssignedVendor?.Name,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
