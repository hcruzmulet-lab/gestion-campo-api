using GestorCampo.Application.Common;
using GestorCampo.Application.Sync.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Providers;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Models;

namespace GestorCampo.Application.Sync;

public class SyncService
{
    private readonly IClientProvider _clientProvider;
    private readonly IProductProvider _productProvider;
    private readonly IOrderExporter _orderExporter;
    private readonly IClientRepository _clients;
    private readonly IProductRepository _products;
    private readonly ISyncLogRepository _syncLogs;

    public SyncService(
        IClientProvider clientProvider,
        IProductProvider productProvider,
        IOrderExporter orderExporter,
        IClientRepository clients,
        IProductRepository products,
        ISyncLogRepository syncLogs)
    {
        _clientProvider = clientProvider;
        _productProvider = productProvider;
        _orderExporter = orderExporter;
        _clients = clients;
        _products = products;
        _syncLogs = syncLogs;
    }

    public async Task SyncClientsAsync(CancellationToken ct = default)
    {
        var log = new SyncLog { Adapter = "ClientProvider", Entity = "clients", Status = "running", StartedAt = DateTime.UtcNow };
        await _syncLogs.AddAsync(log, ct);

        try
        {
            var items = await _clientProvider.FetchAsync(ct);
            var processed = 0;

            foreach (var item in items)
            {
                var existing = await _clients.GetByExternalIdAsync(item.ExternalId, ct);
                if (existing != null)
                {
                    existing.Name = item.Name;
                    existing.TaxId = item.TaxId;
                    existing.Address = item.Address;
                    existing.Phone = item.Phone;
                    existing.Email = item.Email;
                    existing.Source = "external";
                    await _clients.UpdateAsync(existing, ct);
                }
                else
                {
                    await _clients.AddAsync(new Client
                    {
                        ExternalId = item.ExternalId,
                        Name = item.Name,
                        TaxId = item.TaxId,
                        Address = item.Address,
                        Phone = item.Phone,
                        Email = item.Email,
                        Source = "external"
                    }, ct);
                }
                processed++;
            }

            log.Status = "success";
            log.ItemsProcessed = processed;
            log.FinishedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            log.Status = "failed";
            log.Error = ex.Message;
            log.FinishedAt = DateTime.UtcNow;
        }

        await _syncLogs.UpdateAsync(log, ct);
    }

    public async Task SyncProductsAsync(CancellationToken ct = default)
    {
        var log = new SyncLog { Adapter = "ProductProvider", Entity = "products", Status = "running", StartedAt = DateTime.UtcNow };
        await _syncLogs.AddAsync(log, ct);

        try
        {
            var items = await _productProvider.FetchAsync(ct);
            var processed = 0;

            foreach (var item in items)
            {
                var existing = await _products.GetByExternalIdAsync(item.ExternalId, ct);
                if (existing != null)
                {
                    existing.Name = item.Name;
                    existing.Code = item.Code;
                    existing.Price = item.Price;
                    existing.Description = item.Description;
                    existing.Category = item.Category;
                    existing.Stock = item.Stock;
                    existing.Source = "external";
                    await _products.UpdateAsync(existing, ct);
                }
                else
                {
                    await _products.AddAsync(new Product
                    {
                        ExternalId = item.ExternalId,
                        Name = item.Name,
                        Code = item.Code,
                        Price = item.Price,
                        Description = item.Description,
                        Category = item.Category,
                        Stock = item.Stock,
                        Source = "external"
                    }, ct);
                }
                processed++;
            }

            log.Status = "success";
            log.ItemsProcessed = processed;
            log.FinishedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            log.Status = "failed";
            log.Error = ex.Message;
            log.FinishedAt = DateTime.UtcNow;
        }

        await _syncLogs.UpdateAsync(log, ct);
    }

    public async Task<bool> ExportOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var log = new SyncLog { Adapter = "OrderExporter", Entity = "orders", Status = "running", StartedAt = DateTime.UtcNow };
        await _syncLogs.AddAsync(log, ct);

        try
        {
            var success = await _orderExporter.ExportAsync(orderId, ct);
            log.Status = success ? "success" : "failed";
            log.ItemsProcessed = success ? 1 : 0;
            log.FinishedAt = DateTime.UtcNow;
            await _syncLogs.UpdateAsync(log, ct);
            return success;
        }
        catch (Exception ex)
        {
            log.Status = "failed";
            log.Error = ex.Message;
            log.FinishedAt = DateTime.UtcNow;
            await _syncLogs.UpdateAsync(log, ct);
            return false;
        }
    }

    public async Task<ServiceResult<PagedResult<SyncLogResponse>>> GetLogsAsync(
        SyncLogListRequest request, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var (items, totalCount) = await _syncLogs.GetListAsync(
            request.Page, pageSize,
            request.Adapter, request.Status,
            request.From, request.To, ct);

        return ServiceResult<PagedResult<SyncLogResponse>>.Ok(new PagedResult<SyncLogResponse>
        {
            Items = items.Select(l => new SyncLogResponse
            {
                Id = l.Id,
                Adapter = l.Adapter,
                Entity = l.Entity,
                Status = l.Status,
                Error = l.Error,
                ItemsProcessed = l.ItemsProcessed,
                StartedAt = l.StartedAt,
                FinishedAt = l.FinishedAt
            }).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }
}
