using GestorCampo.Application.Common;
using GestorCampo.Application.Visits.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.Visits;

public class VisitService
{
    private readonly IVisitRepository _visits;
    private readonly IClientRepository _clients;
    private readonly IUserRepository _users;

    public VisitService(IVisitRepository visits, IClientRepository clients, IUserRepository users)
    {
        _visits = visits;
        _clients = clients;
        _users = users;
    }

    public async Task<ServiceResult<VisitResponse>> CreateAsync(
        CreateVisitRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var client = await _clients.GetByIdAsync(request.ClientId, ct);
        if (client == null)
            return ServiceResult<VisitResponse>.Fail("Cliente no encontrado");

        Guid vendorId;
        if (currentRole == UserRole.Vendor)
        {
            vendorId = currentUserId;
        }
        else
        {
            if (!request.VendorId.HasValue)
                return ServiceResult<VisitResponse>.Fail("Debe especificar un vendedor");

            var vendor = await _users.GetByIdAsync(request.VendorId.Value, ct);
            if (vendor == null || vendor.Role != UserRole.Vendor)
                return ServiceResult<VisitResponse>.Fail("El vendedor especificado no es válido");

            vendorId = request.VendorId.Value;
        }

        var visit = new Visit
        {
            ClientId = request.ClientId,
            Client = client,
            VendorId = vendorId,
            Vendor = null!,
            PlannedById = currentUserId,
            PlannedBy = null!,
            PlannedAt = request.PlannedAt,
            Notes = request.Notes,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId
        };

        await _visits.AddAsync(visit, ct);
        return ServiceResult<VisitResponse>.Ok(ToResponse(visit));
    }

    public async Task<ServiceResult<VisitResponse>> GetByIdAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null)
            return ServiceResult<VisitResponse>.Fail("Visita no encontrada");

        if (currentRole == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<VisitResponse>.Fail("No tiene acceso a esta visita");

        return ServiceResult<VisitResponse>.Ok(ToResponse(visit));
    }

    public async Task<ServiceResult<PagedResult<VisitResponse>>> GetListAsync(
        VisitListRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var vendorFilter = currentRole == UserRole.Vendor ? currentUserId : request.VendorId;

        var (items, totalCount) = await _visits.GetListAsync(
            request.Page, pageSize,
            request.Status, vendorFilter, request.ClientId,
            request.From, request.To, ct);

        return ServiceResult<PagedResult<VisitResponse>>.Ok(new PagedResult<VisitResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult<VisitResponse>> CheckInAsync(
        Guid id, CheckInRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null)
            return ServiceResult<VisitResponse>.Fail("Visita no encontrada");

        if (visit.VendorId != currentUserId)
            return ServiceResult<VisitResponse>.Fail("No tiene acceso a esta visita");

        if (visit.Status != VisitStatus.Planned)
            return ServiceResult<VisitResponse>.Fail("La visita no está planificada");

        visit.Status = VisitStatus.InProgress;
        visit.CheckinAt = DateTime.UtcNow;
        visit.Lat = request.Lat;
        visit.Lng = request.Lng;
        visit.UpdatedBy = currentUserId;

        await _visits.UpdateAsync(visit, ct);
        return ServiceResult<VisitResponse>.Ok(ToResponse(visit));
    }

    public async Task<ServiceResult<VisitResponse>> CheckOutAsync(
        Guid id, CheckOutRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null)
            return ServiceResult<VisitResponse>.Fail("Visita no encontrada");

        if (visit.VendorId != currentUserId)
            return ServiceResult<VisitResponse>.Fail("No tiene acceso a esta visita");

        if (visit.Status != VisitStatus.InProgress)
            return ServiceResult<VisitResponse>.Fail("La visita no está en curso");

        if (request.FinalStatus == VisitStatus.NotCompleted && string.IsNullOrWhiteSpace(request.Comment))
            return ServiceResult<VisitResponse>.Fail("El comentario es obligatorio cuando la visita no se realiza");

        visit.Status = request.FinalStatus;
        visit.CheckoutAt = DateTime.UtcNow;
        visit.Notes = request.Notes;
        visit.Result = request.Result;
        visit.Comment = request.Comment;
        visit.UpdatedBy = currentUserId;

        await _visits.UpdateAsync(visit, ct);
        return ServiceResult<VisitResponse>.Ok(ToResponse(visit));
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null)
            return ServiceResult.Fail("Visita no encontrada");

        visit.DeletedAt = DateTime.UtcNow;
        visit.DeletedBy = currentUserId;
        visit.IsActive = false;
        visit.UpdatedBy = currentUserId;
        await _visits.UpdateAsync(visit, ct);
        return ServiceResult.Ok();
    }

    private static VisitResponse ToResponse(Visit v) => new()
    {
        Id = v.Id,
        ClientId = v.ClientId,
        ClientName = v.Client?.Name ?? string.Empty,
        VendorId = v.VendorId,
        VendorName = v.Vendor?.Name ?? string.Empty,
        PlannedById = v.PlannedById,
        PlannedAt = v.PlannedAt,
        Status = v.Status,
        CheckinAt = v.CheckinAt,
        CheckoutAt = v.CheckoutAt,
        Notes = v.Notes,
        Result = v.Result,
        Comment = v.Comment,
        Lat = v.Lat,
        Lng = v.Lng,
        RelatedOrderId = v.RelatedOrderId,
        IsActive = v.IsActive,
        CreatedAt = v.CreatedAt,
        UpdatedAt = v.UpdatedAt
    };
}
