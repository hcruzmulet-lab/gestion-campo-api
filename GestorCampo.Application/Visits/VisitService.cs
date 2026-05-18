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
    private readonly GeofenceService _geofence;
    private const int GeofenceThresholdMeters = 200;

    public VisitService(IVisitRepository visits, IClientRepository clients, IUserRepository users, GeofenceService geofence)
    {
        _visits = visits;
        _clients = clients;
        _users = users;
        _geofence = geofence;
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

        // HARD rule: a vendor can only have ONE in-progress visit at a time.
        if (await _visits.HasInProgressForVendorAsync(vendorId, ct))
            return ServiceResult<VisitResponse>.Fail(
                "Tenés una visita en curso. Termínala antes de iniciar otra.");

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
        Guid id, CheckInRequest req, Guid currentUserId, UserRole role, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null) return ServiceResult<VisitResponse>.Fail("Visita no encontrada");
        if (role == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<VisitResponse>.Fail("No tiene acceso a esta visita");
        if (visit.Status != VisitStatus.Planned)
            return ServiceResult<VisitResponse>.Fail("Solo se puede check-in en visitas planificadas");

        // HARD rule: a vendor can only have ONE in-progress visit at a time.
        if (await _visits.HasInProgressForVendorAsync(visit.VendorId, ct))
            return ServiceResult<VisitResponse>.Fail(
                "Tenés una visita en curso. Termínala antes de iniciar otra.");

        var now = DateTime.UtcNow;
        visit.CheckInLat = req.Lat;
        visit.CheckInLng = req.Lng;
        visit.CheckinAt = now;
        visit.Status = VisitStatus.InProgress;

        var client = await _clients.GetByIdAsync(visit.ClientId, ct);
        if (client?.Lat is double cLat && client.Lng is double cLng)
        {
            var (within, distance) = _geofence.Compute(cLat, cLng, req.Lat, req.Lng, GeofenceThresholdMeters);
            visit.IsOutOfRange = !within;
            visit.OutOfRangeMeters = distance;
        }

        visit.UpdatedBy = currentUserId;
        await _visits.UpdateAsync(visit, ct);
        return ServiceResult<VisitResponse>.Ok(ToResponse(visit));
    }

    public async Task<ServiceResult<VisitResponse>> CheckOutAsync(
        Guid id, CheckOutRequest req, Guid currentUserId, UserRole role, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null) return ServiceResult<VisitResponse>.Fail("Visita no encontrada");
        if (role == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<VisitResponse>.Fail("No tiene acceso a esta visita");
        if (visit.Status != VisitStatus.InProgress)
            return ServiceResult<VisitResponse>.Fail("Solo se puede check-out en visitas en curso");

        var now = DateTime.UtcNow;
        visit.CheckOutLat = req.Lat;
        visit.CheckOutLng = req.Lng;
        visit.CheckOutAt = now;
        visit.CheckoutAt = now;
        visit.Status = VisitStatus.Completed;
        visit.UpdatedBy = currentUserId;
        await _visits.UpdateAsync(visit, ct);
        return ServiceResult<VisitResponse>.Ok(ToResponse(visit));
    }

    public async Task<ServiceResult<VisitResponse>> UpdateAsync(
        Guid id, UpdateVisitRequest req, Guid currentUserId, UserRole role, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null) return ServiceResult<VisitResponse>.Fail("Visita no encontrada");
        if (role == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<VisitResponse>.Fail("No tiene acceso a esta visita");
        if (visit.Status != VisitStatus.InProgress && visit.Status != VisitStatus.Planned)
            return ServiceResult<VisitResponse>.Fail("Solo se puede actualizar visitas planificadas o en curso");

        if (req.Notes != null) visit.Notes = req.Notes;
        visit.UpdatedBy = currentUserId;
        await _visits.UpdateAsync(visit, ct);
        return ServiceResult<VisitResponse>.Ok(ToResponse(visit));
    }

    public async Task<ServiceResult<VisitResponse>> MarkNotCompletedAsync(
        Guid id, MarkNotCompletedRequest req, Guid currentUserId, UserRole role, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(id, ct);
        if (visit == null) return ServiceResult<VisitResponse>.Fail("Visita no encontrada");
        if (role == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<VisitResponse>.Fail("No tiene acceso a esta visita");
        if (visit.Status != VisitStatus.Planned && visit.Status != VisitStatus.InProgress)
            return ServiceResult<VisitResponse>.Fail(
                "Solo se puede marcar como no realizada una visita en estado planificada o en curso");
        if (req.Reason == VisitNotCompletedReason.Other && string.IsNullOrWhiteSpace(req.ReasonNote))
            return ServiceResult<VisitResponse>.Fail("Debe agregar una nota cuando el motivo es Otro");

        var wasInProgress = visit.Status == VisitStatus.InProgress;
        var now = DateTime.UtcNow;
        visit.NotCompletedReason = req.Reason;
        visit.NotCompletedReasonNote = string.IsNullOrWhiteSpace(req.ReasonNote) ? null : req.ReasonNote.Trim();
        visit.Status = VisitStatus.NotCompleted;

        if (wasInProgress)
        {
            visit.CheckoutAt = now;
            visit.CheckOutAt = now;
            if (req.Lat.HasValue) visit.CheckOutLat = req.Lat;
            if (req.Lng.HasValue) visit.CheckOutLng = req.Lng;
        }

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
        UpdatedAt = v.UpdatedAt,
        CheckInLat = v.CheckInLat,
        CheckInLng = v.CheckInLng,
        CheckOutLat = v.CheckOutLat,
        CheckOutLng = v.CheckOutLng,
        CheckOutAtUtc = v.CheckOutAt,
        IsOutOfRange = v.IsOutOfRange,
        OutOfRangeMeters = v.OutOfRangeMeters,
        NotCompletedReason = v.NotCompletedReason,
        NotCompletedReasonNote = v.NotCompletedReasonNote
    };
}
