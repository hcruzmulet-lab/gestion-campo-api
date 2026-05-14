using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Services;
using GestorCampo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.API.DevSeed;

public static class DashboardDevSeed
{
    // Buenos Aires city center as base coordinates
    private const double BaseLat = -34.603;
    private const double BaseLng = -58.382;

    private static (double lat, double lng) Offset(double lat, double lng, double meters, double bearingDeg)
    {
        var bearing = bearingDeg * Math.PI / 180;
        var dLat = meters * Math.Cos(bearing) / 111320.0;
        var dLng = meters * Math.Sin(bearing) / (111320.0 * Math.Cos(lat * Math.PI / 180));
        return (lat + dLat, lng + dLng);
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        // Idempotency guard
        if (await db.Users.AnyAsync(u => u.Email.EndsWith("@devvendor.gc")))
            return;

        var rng = new Random(42);

        // ── Products ──────────────────────────────────────────────────────────
        var products = await db.Products.Take(5).ToListAsync();
        if (products.Count < 3)
        {
            var newProducts = new[]
            {
                new Product { Id = Guid.NewGuid(), Name = "Bebida Cola 2L",    Code = "BEV001", Price = 850m,  Stock = 200 },
                new Product { Id = Guid.NewGuid(), Name = "Agua Mineral 1.5L", Code = "BEV002", Price = 320m,  Stock = 500 },
                new Product { Id = Guid.NewGuid(), Name = "Jugo Naranja 1L",   Code = "BEV003", Price = 490m,  Stock = 300 },
                new Product { Id = Guid.NewGuid(), Name = "Galletitas 200g",   Code = "SNK001", Price = 280m,  Stock = 400 },
                new Product { Id = Guid.NewGuid(), Name = "Alfajores x3",      Code = "SNK002", Price = 450m,  Stock = 250 },
            };
            // Set audit fields using a temp ID (products don't need real owner for dev seed)
            var tempId = Guid.NewGuid();
            foreach (var p in newProducts) { p.CreatedBy = tempId; p.UpdatedBy = tempId; }
            await db.Products.AddRangeAsync(newProducts);
            await db.SaveChangesAsync();
            products = newProducts.ToList();
        }

        // ── Supervisor ────────────────────────────────────────────────────────
        var supervisorId = Guid.NewGuid();
        var supervisor = new User
        {
            Id = supervisorId,
            Name = "Supervisor Dev",
            Email = "supervisor@devvendor.gc",
            PasswordHash = passwordService.Hash("Dev1234!"),
            Role = UserRole.Supervisor,
            IsActive = true,
            EmailVerified = true,
            CreatedBy = supervisorId,
            UpdatedBy = supervisorId,
        };
        await db.Users.AddAsync(supervisor);

        // ── Vendors ───────────────────────────────────────────────────────────
        var vendorDefs = new[]
        {
            ("Juan Rodríguez",  "juan@devvendor.gc"),
            ("Ana Martínez",    "ana@devvendor.gc"),
            ("Pedro López",     "pedro@devvendor.gc"),
            ("Sofía Torres",    "sofia@devvendor.gc"),
            ("Carlos Gómez",    "carlos@devvendor.gc"),
            ("Valentina Ruiz",  "valentina@devvendor.gc"),
        };

        var vendors = vendorDefs.Select(v =>
        {
            var id = Guid.NewGuid();
            return new User
            {
                Id = id,
                Name = v.Item1,
                Email = v.Item2,
                PasswordHash = passwordService.Hash("Dev1234!"),
                Role = UserRole.Vendor,
                IsActive = true,
                EmailVerified = true,
                CreatedBy = supervisorId,
                UpdatedBy = supervisorId,
            };
        }).ToList();
        await db.Users.AddRangeAsync(vendors);

        // ── Clients with lat/lng ──────────────────────────────────────────────
        var clientData = new[]
        {
            ("Supermercado Norte",    "SUP001", 400.0,  30.0),
            ("Distribuidora Central", "DIS001", 800.0,  75.0),
            ("Almacén La Esquina",    "ALM001", 1200.0, 120.0),
            ("Minimarket 24hs",       "MIN001", 600.0,  200.0),
            ("Kiosco Del Parque",     "KIO001", 1500.0, 250.0),
            ("Ferretería Omega",      "FER001", 900.0,  310.0),
            ("Farmacia San Martín",   "FAR001", 700.0,  355.0),
            ("Verdulería El Huerto",  "VER001", 1100.0, 45.0),
            ("Panadería Artesanal",   "PAN001", 300.0,  160.0),
            ("Librería Central",      "LIB001", 1800.0, 220.0),
            ("Carnicería La Mejor",   "CAR001", 500.0,  290.0),
            ("Heladería Venezia",     "HEL001", 1300.0, 335.0),
        };

        var clients = clientData.Select(c =>
        {
            var (lat, lng) = Offset(BaseLat, BaseLng, c.Item3, c.Item4);
            return new Client
            {
                Id = Guid.NewGuid(),
                Name = c.Item1,
                TaxId = c.Item2,
                Address = $"Calle {rng.Next(1, 999)} N°{rng.Next(100, 9999)}, CABA",
                Phone = $"011-{rng.Next(4000, 5999)}-{rng.Next(1000, 9999)}",
                Email = $"contacto@{c.Item2.ToLower()}.com",
                IsActive = true,
                Lat = lat,     // Client entity uses Lat/Lng (not Latitude/Longitude)
                Lng = lng,
                Category = c.Item4 < 180 ? "Supermercados" : "Minoristas",
                CreatedBy = supervisorId,
                UpdatedBy = supervisorId,
            };
        }).ToList();
        await db.Clients.AddRangeAsync(clients);
        await db.SaveChangesAsync();

        // ── Visits + Orders for today ─────────────────────────────────────────
        var today = DateTime.UtcNow.Date;

        var visitPlans = new[]
        {
            (vendors[0], new[] { VisitStatus.Completed, VisitStatus.Completed, VisitStatus.Completed, VisitStatus.InProgress, VisitStatus.Planned }),
            (vendors[1], new[] { VisitStatus.Completed, VisitStatus.Completed, VisitStatus.NotCompleted, VisitStatus.InProgress, VisitStatus.Planned }),
            (vendors[2], new[] { VisitStatus.Completed, VisitStatus.InProgress, VisitStatus.Planned, VisitStatus.Planned }),
            (vendors[3], new[] { VisitStatus.Completed, VisitStatus.Completed, VisitStatus.Planned }),
            (vendors[4], new[] { VisitStatus.Planned, VisitStatus.Planned }),
            (vendors[5], new[] { VisitStatus.Completed, VisitStatus.NotCompleted }),
        };

        var allVisits = new List<Visit>();
        var allOrders = new List<Order>();

        foreach (var (vendor, statuses) in visitPlans)
        {
            var clientQueue = clients.OrderBy(_ => rng.Next()).ToList();
            for (var i = 0; i < statuses.Length; i++)
            {
                var client = clientQueue[i % clientQueue.Count];
                var status = statuses[i];
                var checkin = status is VisitStatus.InProgress or VisitStatus.Completed or VisitStatus.NotCompleted
                    ? today.AddHours(8 + i * 1.5) : (DateTime?)null;
                var checkout = status is VisitStatus.Completed or VisitStatus.NotCompleted
                    ? checkin!.Value.AddMinutes(rng.Next(20, 60)) : (DateTime?)null;

                var visit = new Visit
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    Client = client,
                    VendorId = vendor.Id,
                    PlannedById = supervisorId,
                    PlannedAt = today.AddHours(8 + i * 1.5),
                    Status = status,
                    CheckinAt = checkin,
                    CheckoutAt = checkout,
                    Lat = client.Lat,
                    Lng = client.Lng,
                    CreatedBy = supervisorId,
                    UpdatedBy = supervisorId,
                };
                allVisits.Add(visit);

                if (status == VisitStatus.Completed && rng.Next(10) < 7)
                {
                    var lineCount = rng.Next(2, 5);
                    var orderLines = products
                        .OrderBy(_ => rng.Next())
                        .Take(lineCount)
                        .Select(p => new OrderLine
                        {
                            Id = Guid.NewGuid(),
                            ProductId = p.Id,
                            Quantity = rng.Next(1, 10),
                            UnitPrice = p.Price,
                            Discount = new[] { 0m, 0m, 0.05m, 0.10m }[rng.Next(4)],
                        }).ToList();

                    var order = new Order
                    {
                        Id = Guid.NewGuid(),
                        ClientId = client.Id,
                        VendorId = vendor.Id,
                        VisitId = visit.Id,
                        Status = OrderStatus.Approved,
                        ApprovedAt = checkout!.Value.AddMinutes(10),
                        Lines = orderLines,
                        CreatedBy = supervisorId,
                        UpdatedBy = supervisorId,
                    };
                    visit.RelatedOrderId = order.Id;
                    allOrders.Add(order);
                }
            }
        }

        await db.Visits.AddRangeAsync(allVisits);
        await db.Orders.AddRangeAsync(allOrders);
        await db.SaveChangesAsync();

        // ── Tracking points ───────────────────────────────────────────────────
        var activeVendors = vendors.Take(4).ToList();
        var trackingPoints = new List<TrackingPoint>();

        foreach (var vendor in activeVendors)
        {
            var lat = BaseLat + (rng.NextDouble() - 0.5) * 0.02;
            var lng = BaseLng + (rng.NextDouble() - 0.5) * 0.02;
            var startTime = today.AddHours(7.5);

            for (var step = 0; step < 50; step++)
            {
                var bearing = rng.NextDouble() * 120 - 60 + step * 3;
                (lat, lng) = Offset(lat, lng, 150 + rng.NextDouble() * 100, bearing);

                trackingPoints.Add(new TrackingPoint
                {
                    Id = Guid.NewGuid(),
                    VendorId = vendor.Id,
                    Lat = lat,
                    Lng = lng,
                    CapturedAt = startTime.AddMinutes(step * 7.2),
                    SyncedAt = startTime.AddMinutes(step * 7.2 + 1),
                });
            }
        }

        await db.TrackingPoints.AddRangeAsync(trackingPoints);
        await db.SaveChangesAsync();
    }
}
