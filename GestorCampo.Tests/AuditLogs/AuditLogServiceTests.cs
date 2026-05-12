using FluentAssertions;
using GestorCampo.Application.AuditLogs;
using GestorCampo.Application.AuditLogs.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.AuditLogs;

public class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _repo = new();
    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        _sut = new AuditLogService(_repo.Object);
    }

    [Fact]
    public async Task GetList_ReturnsPagedAuditLogs()
    {
        var logs = new List<AuditLog>
        {
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Action = "POST", Module = "Visits", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Action = "POST", Module = "Orders", CreatedAt = DateTime.UtcNow }
        };
        _repo.Setup(r => r.GetListAsync(1, 20, null, null, null, null, default))
            .ReturnsAsync((logs, 2));

        var result = await _sut.GetListAsync(new AuditLogListRequest());

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetList_PassesFiltersToRepo()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetListAsync(1, 20, userId, "Visits", null, null, default))
            .ReturnsAsync((new List<AuditLog>(), 0));

        await _sut.GetListAsync(new AuditLogListRequest { UserId = userId, Module = "Visits" });

        _repo.Verify(r => r.GetListAsync(1, 20, userId, "Visits", null, null, default), Times.Once);
    }
}
