using GestorCampo.Application.Common;
using GestorCampo.Application.Visits.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Interfaces.Services;

namespace GestorCampo.Application.Visits;

public class VisitAttachmentService
{
    private const int MaxPerVisit = 10;
    private static readonly TimeSpan PresignedTtl = TimeSpan.FromMinutes(15);

    private readonly IVisitRepository _visits;
    private readonly IVisitAttachmentRepository _attachments;
    private readonly IFileStorage _storage;

    public VisitAttachmentService(IVisitRepository visits,
                                  IVisitAttachmentRepository attachments,
                                  IFileStorage storage)
    {
        _visits = visits;
        _attachments = attachments;
        _storage = storage;
    }

    public async Task<ServiceResult<VisitAttachmentResponse>> UploadAsync(
        Guid visitId, UploadAttachmentRequest req, Guid currentUserId, UserRole role,
        CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(visitId, ct);
        if (visit == null)
            return ServiceResult<VisitAttachmentResponse>.Fail("Visita no encontrada");

        if (role == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<VisitAttachmentResponse>.Fail("No tiene acceso a esta visita");

        var count = await _attachments.CountByVisitIdAsync(visitId, ct);
        if (count >= MaxPerVisit)
            return ServiceResult<VisitAttachmentResponse>.Fail($"Se alcanzó el máximo de {MaxPerVisit} fotos por visita");

        var key = $"visits/{visitId}/{Guid.NewGuid()}.jpg";
        await _storage.UploadAsync(req.Content, key, req.ContentType, ct);

        var attachment = new VisitAttachment
        {
            VisitId = visitId,
            StorageKey = key,
            ContentType = req.ContentType,
            SizeBytes = req.SizeBytes,
            Width = req.Width,
            Height = req.Height,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId
        };
        await _attachments.AddAsync(attachment, ct);

        var url = await _storage.GetPresignedReadUrlAsync(key, PresignedTtl, ct);
        return ServiceResult<VisitAttachmentResponse>.Ok(ToResponse(attachment, url));
    }

    public async Task<ServiceResult<List<VisitAttachmentResponse>>> ListAsync(
        Guid visitId, Guid currentUserId, UserRole role, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(visitId, ct);
        if (visit == null)
            return ServiceResult<List<VisitAttachmentResponse>>.Fail("Visita no encontrada");

        if (role == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<List<VisitAttachmentResponse>>.Fail("No tiene acceso a esta visita");

        var items = await _attachments.GetByVisitIdAsync(visitId, ct);
        var result = new List<VisitAttachmentResponse>(items.Count);
        foreach (var a in items)
        {
            var url = await _storage.GetPresignedReadUrlAsync(a.StorageKey, PresignedTtl, ct);
            result.Add(ToResponse(a, url));
        }
        return ServiceResult<List<VisitAttachmentResponse>>.Ok(result);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        Guid visitId, Guid attachmentId, Guid currentUserId, UserRole role, CancellationToken ct = default)
    {
        var visit = await _visits.GetByIdAsync(visitId, ct);
        if (visit == null) return ServiceResult<bool>.Fail("Visita no encontrada");
        if (role == UserRole.Vendor && visit.VendorId != currentUserId)
            return ServiceResult<bool>.Fail("No tiene acceso a esta visita");

        var attachment = await _attachments.GetByIdAsync(attachmentId, ct);
        if (attachment == null || attachment.VisitId != visitId)
            return ServiceResult<bool>.Fail("Adjunto no encontrado");

        await _storage.DeleteAsync(attachment.StorageKey, ct);
        await _attachments.DeleteAsync(attachment, ct);
        return ServiceResult<bool>.Ok(true);
    }

    private static VisitAttachmentResponse ToResponse(VisitAttachment a, string url) => new()
    {
        Id = a.Id,
        VisitId = a.VisitId,
        Url = url,
        ContentType = a.ContentType,
        SizeBytes = a.SizeBytes,
        Width = a.Width,
        Height = a.Height,
        CreatedAt = a.CreatedAt
    };
}
