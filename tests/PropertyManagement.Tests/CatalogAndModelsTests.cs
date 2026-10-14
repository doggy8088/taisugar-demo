using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;
using PropertyManagement.Models;
using PropertyManagement.Services;

namespace PropertyManagement.Tests;

public class CatalogAndModelsTests
{
    [Fact]
    public void Catalog_AllModulesDefined_AndFindReturnsExpectedOrNull()
    {
        Assert.Equal(8, Catalog.All.Length);
        string[] expectedKeys = ["assets", "units", "tenants", "leases", "meters", "readings", "invoices", "payments"];
        Assert.Equal(expectedKeys, Catalog.All.Select(m => m.Key).ToArray());

        foreach (var key in expectedKeys)
        {
            var module = Catalog.Find(key);
            Assert.NotNull(module);
            Assert.Contains(module.Fields, f => f.Key == "Code" && f.Required);
            Assert.Contains(module.Fields, f => f.Key == "Name" && f.Required);
            Assert.Contains(module.Fields, f => f.Key == "Status" && f.Required);
            Assert.Contains(module.Fields, f => f.Key == "Notes" && !f.Required);
        }

        Assert.Null(Catalog.Find("unknown"));
    }

    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(45, 20, 3)]
    public void ListModel_Pages_ComputesTotalPagesCorrectly(int total, int size, int expectedPages)
    {
        var model = new ListModel { Total = total, Size = size };
        Assert.Equal(expectedPages, model.Pages);
    }

    [Fact]
    public void ListModel_Cell_FormatsBalancesReferencesDatesAndDecimals()
    {
        var invoicesModule = Catalog.Find("invoices")!;
        var unitsModule = Catalog.Find("units")!;

        var invoice = new Invoice
        {
            Id = 10,
            Code = "INV-10",
            Name = "租金帳款",
            LeaseId = 99,
            Period = new DateTime(2026, 10, 1),
            DueDate = new DateTime(2026, 10, 5),
            Amount = 32500m
        };

        var invList = new ListModel
        {
            Module = invoicesModule,
            Balances = { [10] = 12500m },
            Lookups =
            {
                ["leases"] = new Dictionary<int, string> { [99] = "LSE-99 · 測試合約" }
            }
        };

        Assert.Equal("12,500", invList.Cell(invoice, "Balance"));
        Assert.Equal("LSE-99 · 測試合約", invList.Cell(invoice, "LeaseId"));
        Assert.Equal("2026-10", invList.Cell(invoice, "Period"));
        Assert.Equal("2026/10/05", invList.Cell(invoice, "DueDate"));
        Assert.Equal("32,500", invList.Cell(invoice, "Amount"));

        var unit = new Unit { Id = 20, Code = "UNT-20", Name = "2F", AssetId = 777, Area = 25.75m };
        var unitList = new ListModel { Module = unitsModule };
        Assert.Equal("—", unitList.Cell(unit, "AssetId"));
        Assert.Equal("25.75", unitList.Cell(unit, "Area"));
        Assert.Equal("—", unitList.Cell(unit, "NonExistentProperty"));
    }

    [Theory]
    [InlineData("王小明", "王○明")]
    [InlineData("王明", "王○")]
    [InlineData("歐陽小明", "歐○○明")]
    [InlineData("王", "○")]
    [InlineData("", "—")]
    public void ListModel_Cell_MasksTenantContactInListView(string contact, string expected)
    {
        var tenantsModule = Catalog.Find("tenants")!;
        var tenantList = new ListModel { Module = tenantsModule };
        var tenant = new Tenant
        {
            Id = 30,
            Code = "TNT-30",
            Name = "澄光設計有限公司",
            Kind = "公司",
            Contact = contact,
            Phone = "02-27100001"
        };

        Assert.Equal(expected, tenantList.Cell(tenant, "Contact"));
        Assert.Equal("公司", tenantList.Cell(tenant, "Kind"));
        Assert.Equal("02-27100001", tenantList.Cell(tenant, "Phone"));
    }

    [Fact]
    public void EditModel_Value_ReturnsStoredValueOrEmptyString()
    {
        var edit = new EditModel
        {
            Values = { ["Code"] = "AST-001" }
        };

        Assert.Equal("AST-001", edit.Value("Code"));
        Assert.Equal("", edit.Value("MissingKey"));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(20, 15, 75.0)]
    [InlineData(3, 2, 66.7)]
    public void DashboardModel_Occupancy_CalculatesPercentageWithOneDecimalPlace(int units, int occupied, decimal expected)
    {
        var model = new DashboardModel { Units = units, Occupied = occupied };
        Assert.Equal(expected, model.Occupancy);
    }

    [Fact]
    public void ErrorViewModel_ShowRequestId_ReflectsRequestIdPresence()
    {
        Assert.False(new ErrorViewModel { RequestId = null }.ShowRequestId);
        Assert.False(new ErrorViewModel { RequestId = "" }.ShowRequestId);
        Assert.True(new ErrorViewModel { RequestId = "req-123" }.ShowRequestId);
    }

    [Fact]
    public async Task AppDbContext_StoresDecimalsAsScaledIntegersAndEnforcesConstraints()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var asset = new Asset { Id = 1, Code = "AST-01", Name = "測試資產", Area = 123.45m };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        // 確認底層 SQLite 儲存為放大 100 倍的整數 12345
        await using var cmd = scope.Connection.CreateCommand();
        cmd.CommandText = "SELECT Area FROM Assets WHERE Id = 1";
        var rawValue = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Assert.Equal(12345L, rawValue);

        var loaded = await db.Assets.AsNoTracking().SingleAsync(x => x.Id == 1);
        Assert.Equal(123.45m, loaded.Area);
    }

    [Fact]
    public void SeedData_Initialize_PopulatesDemoDataIdempotently()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        SeedData.Initialize(db);

        Assert.Equal(6, db.Assets.Count());
        Assert.Equal(32, db.Units.Count());
        Assert.Equal(21, db.Tenants.Count());
        Assert.Equal(21, db.Leases.Count());
        Assert.Equal(21, db.Meters.Count());
        Assert.Equal(21, db.Readings.Count());
        Assert.Equal(126, db.Invoices.Count());
        Assert.True(db.Payments.Any());

        var paymentCount = db.Payments.Count();

        // 再次呼叫不會重複塞入資料
        SeedData.Initialize(db);
        Assert.Equal(6, db.Assets.Count());
        Assert.Equal(paymentCount, db.Payments.Count());
    }
}
