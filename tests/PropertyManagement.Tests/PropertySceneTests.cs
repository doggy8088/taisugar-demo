using PropertyManagement.Models;
using PropertyManagement.Services;

namespace PropertyManagement.Tests;

public class PropertySceneTests
{
    [Fact]
    public void Build_KeepsHistoricalOccupancySeparateFromCurrentOverdueBalance()
    {
        var today = new DateTime(2026, 10, 14);
        Asset[] assets = [new() { Id = 1, Code = "A", Status = "啟用" }];
        Unit[] units = [new() { Id = 2, AssetId = 1, Code = "U", Floor = "", Status = "啟用" }];
        Tenant[] tenants = [new() { Id = 3, Name = "歷史承租戶" }];
        Lease[] leases = [new() { Id = 4, UnitId = 2, TenantId = 3, Status = "有效",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 9, 30), Rent = 10000 }];
        Invoice[] invoices = [new() { Id = 5, LeaseId = 4, Status = "啟用", Amount = 10000, DueDate = new DateTime(2026, 9, 5) }];

        var historical = Assert.Single(PropertySceneService.Build(assets, units, tenants, leases, invoices, [], new DateTime(2026, 9, 30), today));
        var historicalUnit = Assert.Single(historical.Units);
        Assert.Equal("出租中", historicalUnit.Status);
        Assert.Equal("未標示樓層", historicalUnit.Floor);
        Assert.Equal(10000m, historicalUnit.Outstanding);
        Assert.False(historicalUnit.Expiring);

        var current = Assert.Single(PropertySceneService.Build(assets, units, tenants, leases, invoices, [], today, today));
        var currentUnit = Assert.Single(current.Units);
        Assert.Equal("空置中", currentUnit.Status);
        Assert.Null(currentUnit.LeaseId);
        Assert.Equal(10000m, currentUnit.Outstanding);
    }

    [Fact]
    public void Build_PreservesEmptyAssetsAndExcludesDisabledAssetsFromOccupancy()
    {
        var today = new DateTime(2026, 10, 14);
        Asset[] assets = [new() { Id = 1, Code = "A", Status = "停用" }, new() { Id = 2, Code = "B", Status = "啟用" }];
        Unit[] units = [new() { Id = 3, AssetId = 1, Code = "U", Status = "啟用" }];
        Lease[] leases = [new() { Id = 4, UnitId = 3, TenantId = 5, Status = "有效",
            StartDate = today.AddDays(-1), EndDate = today.AddDays(30) }];

        var scene = PropertySceneService.Build(assets, units, [], leases, [], [], today, today);

        Assert.Equal(2, scene.Count);
        Assert.Equal("停用", Assert.Single(scene[0].Units).Status);
        Assert.Equal(0, scene[0].Occupied);
        Assert.Equal(0, scene[0].Available);
        Assert.Empty(scene[1].Units);
        Assert.Empty(PropertySceneService.Build([], [], [], [], [], [], today, today));
    }

    [Fact]
    public void Build_MapsRealUnitsAndLeaseBalancesToOrderedFloors()
    {
        var today = new DateTime(2026, 10, 14);
        Asset[] assets = [new() { Id = 1, Code = "A", Name = "測試大樓", Status = "啟用" }];
        Unit[] units = [
            new() { Id = 2, AssetId = 1, Code = "U2", Name = "二樓", Floor = "2F-1", Status = "啟用" },
            new() { Id = 3, AssetId = 1, Code = "U3", Name = "十樓", Floor = "10F-1", Status = "維修中" },
            new() { Id = 4, AssetId = 1, Code = "U4", Name = "地下室", Floor = "B1-1", Status = "啟用" }
        ];
        Tenant[] tenants = [new() { Id = 5, Name = "測試承租戶" }];
        Lease[] leases = [new() { Id = 6, UnitId = 2, TenantId = 5, Status = "有效",
            StartDate = today.AddMonths(-3), EndDate = today.AddDays(30), Rent = 20000 }];
        Invoice[] invoices = [
            new() { Id = 7, LeaseId = 6, Status = "啟用", Amount = 20000, DueDate = today.AddDays(-1) },
            new() { Id = 8, LeaseId = 6, Status = "作廢", Amount = 90000, DueDate = today.AddDays(-1) },
            new() { Id = 9, LeaseId = 6, Status = "啟用", Amount = 30000, DueDate = today }
        ];
        Payment[] payments = [
            new() { InvoiceId = 7, Amount = 5000, Status = "啟用" },
            new() { InvoiceId = 7, Amount = 8000, Status = "作廢" }
        ];

        var scene = Assert.Single(PropertySceneService.Build(assets, units, tenants, leases,
            invoices, payments, today, today));

        Assert.Equal(new[] { "B1", "2F", "10F" }, scene.Units.Select(x => x.Floor));
        Assert.Equal("空置中", scene.Units[0].Status);
        var rented = scene.Units[1];
        Assert.Equal("出租中", rented.Status);
        Assert.Equal("測試承租戶", rented.Tenant);
        Assert.Equal(6, rented.LeaseId);
        Assert.Equal(20000m, rented.Rent);
        Assert.Equal(15000m, rented.Outstanding);
        Assert.True(rented.Expiring);
        Assert.Equal("維修中", scene.Units[2].Status);
        Assert.Equal(1, scene.Occupied);
        Assert.Equal(1, scene.Available);
        Assert.Equal(15000m, scene.Outstanding);
    }
}
