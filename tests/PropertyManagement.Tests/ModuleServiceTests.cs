using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using PropertyManagement.Models;
using PropertyManagement.Services;

namespace PropertyManagement.Tests;

public class ModuleServiceTests
{
    private static FormCollection ToForm(Dictionary<string, string> values) =>
        new(values.ToDictionary(k => k.Key, v => new StringValues(v.Value)));

    [Fact]
    public async Task List_SupportsSearchFilterSortAndPaginationClamping()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        for (int i = 1; i <= 15; i++)
        {
            db.Assets.Add(new Asset
            {
                Id = i,
                Code = $"AST-{i:00}",
                Name = $"資產 {16 - i:00}",
                Status = i % 2 == 0 ? "維修中" : "啟用",
                Notes = i == 7 ? "特殊備註關鍵字" : "",
                UpdatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var service = new ModuleService<Asset>(db, Catalog.Find("assets")!);

        // 分頁大小無效時正規化為 10，頁碼超出範圍時收斂至最後一頁
        var clamped = await service.List("", "", page: 99, size: 15, sort: "code");
        Assert.Equal(10, clamped.Size);
        Assert.Equal(2, clamped.Page);
        Assert.Equal(15, clamped.Total);
        Assert.Equal(5, clamped.Rows.Count);
        Assert.Equal("AST-11", clamped.Rows[0].Code);

        // 關鍵字搜尋（比對備註）
        var searched = await service.List("特殊備註", "", page: 1, size: 10, sort: "code");
        Assert.Single(searched.Rows);
        Assert.Equal("AST-07", searched.Rows[0].Code);

        // 狀態篩選與無效狀態忽略
        var filtered = await service.List("", "維修中", page: 1, size: 20, sort: "name");
        Assert.Equal(7, filtered.Total);
        Assert.Equal("維修中", filtered.Status);
        Assert.Equal("資產 02", filtered.Rows[0].Name);

        var invalidStatus = await service.List("", "不存在狀態", page: -3, size: 20, sort: "updated");
        Assert.Equal("", invalidStatus.Status);
        Assert.Equal(1, invalidStatus.Page);
        Assert.Equal("AST-15", invalidStatus.Rows[0].Code);
    }

    [Fact]
    public async Task List_ComputesInvoiceBalancesAndUnitOccupancy()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;
        var today = Business.Today;

        db.Assets.Add(new Asset { Id = 1, Code = "AST-01", Name = "大樓", Status = "啟用" });
        db.Units.AddRange(
            new Unit { Id = 2, Code = "UNT-01", Name = "1F", AssetId = 1, Status = "啟用" },
            new Unit { Id = 3, Code = "UNT-02", Name = "2F", AssetId = 1, Status = "啟用" },
            new Unit { Id = 4, Code = "UNT-03", Name = "3F", AssetId = 1, Status = "自用" }
        );
        db.Tenants.Add(new Tenant { Id = 5, Code = "TNT-01", Name = "租戶", Status = "啟用" });
        db.Leases.Add(new Lease
        {
            Id = 6,
            Code = "LSE-01",
            Name = "合約",
            UnitId = 2,
            TenantId = 5,
            Status = "有效",
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(10),
            Rent = 20000m
        });
        db.Invoices.AddRange(
            new Invoice { Id = 7, Code = "INV-01", Name = "啟用帳款", LeaseId = 6, Amount = 20000m, Status = "啟用" },
            new Invoice { Id = 8, Code = "INV-02", Name = "作廢帳款", LeaseId = 6, Kind = "水費", Amount = 5000m, Status = "作廢" }
        );
        db.Payments.AddRange(
            new Payment { Id = 9, Code = "PAY-01", Name = "有效收款", InvoiceId = 7, Amount = 8000m, Status = "啟用" },
            new Payment { Id = 10, Code = "PAY-02", Name = "作廢收款", InvoiceId = 7, Amount = 5000m, Status = "作廢" }
        );
        await db.SaveChangesAsync();

        var unitService = new ModuleService<Unit>(db, Catalog.Find("units")!);
        var unitList = await unitService.List("", "", 1, 10, "code");
        Assert.Equal("出租中", unitList.Occupancy[2]);
        Assert.Equal("空置中", unitList.Occupancy[3]);
        Assert.Equal("自用", unitList.Occupancy[4]);

        var invService = new ModuleService<Invoice>(db, Catalog.Find("invoices")!);
        var invList = await invService.List("", "", 1, 10, "code");
        Assert.Equal(12000m, invList.Balances[7]);
        Assert.Equal(0m, invList.Balances[8]);
    }

    [Fact]
    public async Task Edit_CreateUpdateAndConcurrencyCheck_WorkAsExpected()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;
        var service = new ModuleService<Asset>(db, Catalog.Find("assets")!);

        // 新增畫面預設模型
        var createModel = await service.Edit(0);
        Assert.NotNull(createModel);
        Assert.Equal(0, createModel.Id);
        Assert.Equal("啟用", createModel.Value("Status"));
        Assert.Null(await service.Edit(99999));

        // 儲存新資產
        var saved = await service.Save(0, ToForm(new()
        {
            ["Code"] = "AST-100",
            ["Name"] = "測試大樓",
            ["Category"] = "商辦",
            ["Address"] = "信義路 1 號",
            ["Company"] = "總公司",
            ["Manager"] = "王小明",
            ["Area"] = "88.5",
            ["Status"] = "啟用",
            ["Notes"] = "備註內容"
        }));
        Assert.Empty(saved.Errors);
        Assert.True(saved.Id > 0);

        // 編輯並檢查並行版本衝突
        var editModel = await service.Edit(saved.Id);
        Assert.NotNull(editModel);

        var updated = await service.Save(saved.Id, ToForm(new()
        {
            ["Version"] = editModel.Version,
            ["Code"] = "AST-100",
            ["Name"] = "更新後大樓",
            ["Category"] = "商辦",
            ["Address"] = "信義路 1 號",
            ["Company"] = "總公司",
            ["Manager"] = "王小明",
            ["Area"] = "90.25",
            ["Status"] = "啟用",
            ["Notes"] = ""
        }));
        Assert.Empty(updated.Errors);

        // 使用舊 Version 再次儲存應被攔截
        var stale = await service.Save(saved.Id, ToForm(new()
        {
            ["Version"] = editModel.Version,
            ["Code"] = "AST-100",
            ["Name"] = "過期覆寫",
            ["Category"] = "商辦",
            ["Address"] = "信義路 1 號",
            ["Company"] = "總公司",
            ["Manager"] = "王小明",
            ["Area"] = "90.25",
            ["Status"] = "啟用",
            ["Notes"] = ""
        }));
        Assert.Contains("其他人已修改此筆資料，請重新開啟後再編輯。", stale.Errors);

        // 不存在的 ID 儲存應回傳錯誤
        var missing = await service.Save(99999, ToForm(new() { ["Code"] = "X" }));
        Assert.Contains("資料已不存在，請回到列表重新操作。", missing.Errors);
    }

    [Fact]
    public async Task Save_ValidatesRequiredFormatRangeChoicesReferencesEmailAndDuplicateCode()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        db.Assets.Add(new Asset { Id = 1, Code = "AST-DUP", Name = "既有資產" });
        await db.SaveChangesAsync();

        var assetService = new ModuleService<Asset>(db, Catalog.Find("assets")!);
        var assetErrors = (await assetService.Save(0, ToForm(new()
        {
            ["Code"] = "AST-DUP",
            ["Name"] = "",
            ["Category"] = "非法分類",
            ["Address"] = "地址",
            ["Company"] = "公司",
            ["Manager"] = "管理人",
            ["Area"] = "12.345",
            ["Status"] = "啟用",
            ["Notes"] = new string('A', 2001)
        }))).Errors;

        Assert.Contains("請填寫名稱。", assetErrors);
        Assert.Contains("資產類型選項無效。", assetErrors);
        Assert.Contains("面積（坪）須為 0 至 999,999,999，且最多兩位小數。", assetErrors);
        Assert.Contains("備註不可超過 2000 字。", assetErrors);
        Assert.Contains("此編號已被使用，請改用其他編號。", assetErrors);

        // 檢查 Email 格式
        var tenantService = new ModuleService<Tenant>(db, Catalog.Find("tenants")!);
        var tenantErrors = (await tenantService.Save(0, ToForm(new()
        {
            ["Code"] = "TNT-01",
            ["Name"] = "租戶",
            ["Kind"] = "個人",
            ["Contact"] = "聯絡人",
            ["Phone"] = "0912345678",
            ["Email"] = "invalid-email",
            ["Status"] = "啟用"
        }))).Errors;
        Assert.Contains("電子信箱格式不正確。", tenantErrors);

        // 檢查外鍵 Reference、日期年份範圍與轉型格式錯誤
        var leaseService = new ModuleService<Lease>(db, Catalog.Find("leases")!);
        var leaseErrors = (await leaseService.Save(0, ToForm(new()
        {
            ["Code"] = "LSE-01",
            ["Name"] = "租約",
            ["UnitId"] = "999",
            ["TenantId"] = "not-an-int",
            ["StartDate"] = "1899-01-01",
            ["EndDate"] = "2101-12-31",
            ["Rent"] = "20000",
            ["Deposit"] = "40000",
            ["DueDay"] = "5",
            ["Status"] = "草稿"
        }))).Errors;
        Assert.Contains("請選擇有效的出租單位。", leaseErrors);
        Assert.Contains("承租戶格式不正確。", leaseErrors);
        Assert.Contains("起租日年份須介於 1900 至 2100 年。", leaseErrors);
        Assert.Contains("到期日年份須介於 1900 至 2100 年。", leaseErrors);
    }

    [Fact]
    public async Task Delete_EnforcesHistoryRetentionForeignKeyRestrictionAndVersionCheck()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var assetWithUnit = new Asset { Id = 1, Code = "AST-01", Name = "有關聯資產", Version = "v1" };
        var standaloneAsset = new Asset { Id = 2, Code = "AST-02", Name = "無關聯資產", Version = "v2" };
        var unit = new Unit { Id = 3, Code = "UNT-01", Name = "單位", AssetId = 1, Version = "v3" };
        var tenant = new Tenant { Id = 4, Code = "TNT-01", Name = "租戶", Version = "v4" };
        var lease = new Lease { Id = 5, Code = "LSE-01", Name = "租約", UnitId = 3, TenantId = 4, Rent = 10000m, Version = "v5" };
        var invoice = new Invoice { Id = 6, Code = "INV-01", Name = "帳款", LeaseId = 5, Amount = 10000m, Version = "v6" };
        db.Assets.AddRange(assetWithUnit, standaloneAsset);
        db.Units.Add(unit);
        db.Tenants.Add(tenant);
        db.Leases.Add(lease);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var assetService = new ModuleService<Asset>(db, Catalog.Find("assets")!);
        var invService = new ModuleService<Invoice>(db, Catalog.Find("invoices")!);

        // 不存在資料與版本不符
        Assert.Equal("資料已不存在。", await assetService.Delete(999, "v1"));
        Assert.Equal("資料已變更，請重新確認後再刪除。", await assetService.Delete(2, "wrong-version"));

        // 帳務與抄表不可實體刪除
        Assert.Equal(
            "帳務與抄表紀錄保留歷史，請使用編輯功能將狀態改為作廢或停用。",
            await invService.Delete(6, "v6"));

        // 有關聯紀錄不可刪除
        Assert.Equal(
            "此資料已有關聯紀錄，無法刪除。請改為停用或終止，保留歷史資料。",
            await assetService.Delete(1, "v1"));
        db.ChangeTracker.Clear();

        // 無關聯主檔可正常刪除
        Assert.Null(await assetService.Delete(2, "v2"));
        Assert.Null(await db.Assets.FindAsync(2));
    }
}
