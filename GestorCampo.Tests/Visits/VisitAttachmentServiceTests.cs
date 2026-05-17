using FluentAssertions;
using GestorCampo.Application.Visits;
using GestorCampo.Application.Visits.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Interfaces.Services;
using Moq;
using Xunit;

namespace GestorCampo.Tests.Visits;

public class VisitAttachmentServiceTests
{
    private static (Mock<IVisitRepository>, Mock<IVisitAttachmentRepository>, Mock<IFileStorage>, VisitAttachmentService) Build()
    {
        var visits = new Mock<IVisitRepository>();
        var attachments = new Mock<IVisitAttachmentRepository>();
        var storage = new Mock<IFileStorage>();
        var svc = new VisitAttachmentService(visits.Object, attachments.Object, storage.Object);
        return (visits, attachments, storage, svc);
    }

    [Fact]
    public async Task Upload_VisitNotFound_Fails()
    {
        var (visits, _, _, svc) = Build();
        visits.Setup(v => v.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Visit?)null);

        var result = await svc.UploadAsync(Guid.NewGuid(),
            new UploadAttachmentRequest { Content = new MemoryStream(new byte[10]) },
            Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Upload_VendorAccessingOtherVendorVisit_Forbidden()
    {
        var (visits, _, _, svc) = Build();
        var visit = new Visit { Id = Guid.NewGuid(), VendorId = Guid.NewGuid() };
        visits.Setup(v => v.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await svc.UploadAsync(visit.Id,
            new UploadAttachmentRequest { Content = new MemoryStream(new byte[10]) },
            Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("acceso");
    }

    [Fact]
    public async Task Upload_MaxReached_Fails()
    {
        var (visits, attachments, _, svc) = Build();
        var vendorId = Guid.NewGuid();
        var visit = new Visit { Id = Guid.NewGuid(), VendorId = vendorId };
        visits.Setup(v => v.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);
        attachments.Setup(a => a.CountByVisitIdAsync(visit.Id, default)).ReturnsAsync(10);

        var result = await svc.UploadAsync(visit.Id,
            new UploadAttachmentRequest { Content = new MemoryStream(new byte[10]) },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("máximo");
    }

    [Fact]
    public async Task Upload_Valid_StoresAndReturnsUrl()
    {
        var (visits, attachments, storage, svc) = Build();
        var vendorId = Guid.NewGuid();
        var visit = new Visit { Id = Guid.NewGuid(), VendorId = vendorId };
        visits.Setup(v => v.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);
        attachments.Setup(a => a.CountByVisitIdAsync(visit.Id, default)).ReturnsAsync(0);
        storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                                         "image/jpeg", default))
               .ReturnsAsync((Stream s, string key, string _, CancellationToken __) => key);
        storage.Setup(s => s.GetPresignedReadUrlAsync(It.IsAny<string>(),
                                                     It.IsAny<TimeSpan>(), default))
               .ReturnsAsync("https://signed.example/foo");

        var result = await svc.UploadAsync(visit.Id,
            new UploadAttachmentRequest
            {
                Content = new MemoryStream(new byte[10]),
                ContentType = "image/jpeg",
                SizeBytes = 10
            },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        result.Data!.Url.Should().Be("https://signed.example/foo");
        attachments.Verify(a => a.AddAsync(It.IsAny<VisitAttachment>(), default), Times.Once);
    }
}
