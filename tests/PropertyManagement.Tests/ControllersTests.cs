using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using PropertyManagement.Controllers;
using PropertyManagement.Data;
using PropertyManagement.Models;
using PropertyManagement.Services;

namespace PropertyManagement.Tests;

public class ControllersTests
{
    private sealed class DummyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static ManageController CreateManageController(AppDbContext db, Dictionary<string, string>? formValues = null)
    {
        var httpContext = new DefaultHttpContext();
        if (formValues != null)
        {
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Form = new FormCollection(
                formValues.ToDictionary(k => k.Key, v => new StringValues(v.Value)));
        }

        return new ManageController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new DummyTempDataProvider())
        };
    }

    [Fact]
    public async Task HomeController_Index_ComputesDashboardMetricsTrendsExpiringAndOverdue()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;
        var today = Business.Today;
        var currentMonth = new DateTime(today.Year, today.Month, 1);

        db.Assets.AddRange(
            new Asset { Id = 1, Code = "AST-01", Name = "A棟", Category = "商辦", Status = "啟用" },
            new Asset { Id = 2, Code = "AST-02", Name = "B棟", Category = "住宅", Status = "停用" }
        );
        db.Units.AddRange(
            new Unit { Id = 3, Code = "UNT-01", Name = "A-1F", AssetId = 1, Status = "啟用" },
            new Unit { Id = 4, Code = "UNT-02", Name = "A-2F", AssetId = 1, Status = "啟用" },
            new Unit { Id = 5, Code = "UNT-03", Name = "A-3F", AssetId = 1, Status = "維修中" },
            new Unit { Id = 6, Code = "UNT-04", Name = "B-1F", AssetId = 2, Status = "啟用" } // 所屬資產停用，不計入分母
        );
        db.Tenants.Add(new Tenant { Id = 7, Code = "TNT-01", Name = "甲公司", Status = "啟用" });
        db.Leases.Add(new Lease
        {
            Id = 8,
            Code = "LSE-01",
            Name = "甲合約",
            UnitId = 3,
            TenantId = 7,
            Status = "有效",
            StartDate = currentMonth.AddMonths(-2),
            EndDate = today.AddDays(30), // 90 天內到期
            Rent = 30000m,
            DueDay = 5
        });
        db.Invoices.AddRange(
            new Invoice
            {
                Id = 9,
                Code = "INV-CUR",
                Name = "本月租金",
                LeaseId = 8,
                Kind = "租金",
                Period = currentMonth,
                DueDate = today.AddDays(-2), // 已逾期
                Amount = 30000m,
                Status = "啟用"
            },
            new Invoice
            {
                Id = 10,
                Code = "INV-WATER",
                Name = "本月水費",
                LeaseId = 8,
                Kind = "水費",
                Period = currentMonth,
                DueDate = today.AddDays(5),
                Amount = 1000m,
                Status = "啟用"
            }
        );
        db.Payments.Add(new Payment
        {
            Id = 11,
            Code = "PAY-PARTIAL",
            Name = "部分收款",
            InvoiceId = 9,
            PaidDate = currentMonth.AddDays(1),
            Amount = 10000m,
            Status = "啟用"
        });
        await db.SaveChangesAsync();

        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var viewResult = Assert.IsType<ViewResult>(await controller.Index(null));
        var model = Assert.IsType<DashboardModel>(viewResult.Model);

        Assert.Equal(currentMonth, model.Month);
        Assert.Equal(2, model.Assets);
        Assert.Equal(2, model.Units); // 僅 UNT-01 與 UNT-02 為啟用資產下的啟用單位
        Assert.Equal(1, model.Occupied); // UNT-01 出租中
        Assert.Equal(50.0m, model.Occupancy);
        Assert.Equal(30000m, model.Expected); // 僅計租金應收
        Assert.Equal(10000m, model.Received); // 租金實收
        Assert.Equal(20000m, model.Outstanding); // 逾期未收餘額
        Assert.Equal(6, model.Trends.Count);
        Assert.Single(model.Expiring);
        Assert.Single(model.Unpaid);
        Assert.Equal(2, model.Portfolio.Count);
        Assert.Equal(today, model.SceneSnapshot);
        Assert.Equal(2, model.SceneAssets.Count);
        var sceneUnit = Assert.Single(model.SceneAssets[0].Units, x => x.Id == 3);
        Assert.Equal("甲公司", sceneUnit.Tenant);
        Assert.Equal(20000m, sceneUnit.Outstanding);
        Assert.Equal("停用", Assert.Single(model.SceneAssets[1].Units).Status);

        // 測試指定歷史月份與 Error Action
        var pastResult = Assert.IsType<ViewResult>(await controller.Index(currentMonth.AddMonths(-1).ToString("yyyy-MM")));
        var pastModel = Assert.IsType<DashboardModel>(pastResult.Model);
        Assert.Equal(currentMonth.AddMonths(-1), pastModel.Month);
        Assert.Equal(currentMonth.AddDays(-1), pastModel.SceneSnapshot);

        var errorResult = Assert.IsType<ViewResult>(controller.Error());
        var errorModel = Assert.IsType<ErrorViewModel>(errorResult.Model);
        Assert.True(errorModel.ShowRequestId);
    }

    [Fact]
    public async Task ManageController_CrudActions_HandleNotFoundValidationAndSuccess()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var controller = CreateManageController(db);

        // 未知模組一律回傳 404
        Assert.IsType<NotFoundResult>(await controller.Index("unknown"));
        Assert.IsType<NotFoundResult>(await controller.Edit("unknown", 0));
        Assert.IsType<NotFoundResult>(await controller.Save("unknown", 0));
        Assert.IsType<NotFoundResult>(await controller.Delete("unknown", 1));
        Assert.IsType<NotFoundResult>(await controller.ConfirmDelete("unknown", 1, "v"));

        // 驗證失敗時回傳 422 與 Edit 視圖
        var invalidSaveCtrl = CreateManageController(db, new Dictionary<string, string>
        {
            ["Code"] = "",
            ["Name"] = ""
        });
        var invalidResult = Assert.IsType<ViewResult>(await invalidSaveCtrl.Save("assets", 0));
        Assert.Equal(422, invalidSaveCtrl.Response.StatusCode);
        Assert.Equal("Edit", invalidResult.ViewName);

        // 驗證通過時儲存並重導向至 Index
        var validSaveCtrl = CreateManageController(db, new Dictionary<string, string>
        {
            ["Code"] = "AST-CTRL",
            ["Name"] = "控制器測試資產",
            ["Category"] = "商辦",
            ["Address"] = "測試地址",
            ["Company"] = "測試公司",
            ["Manager"] = "測試管理人",
            ["Area"] = "50",
            ["Status"] = "啟用",
            ["Notes"] = ""
        });
        var redirect = Assert.IsType<RedirectToActionResult>(await validSaveCtrl.Save("assets", 0));
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("資產已儲存。", validSaveCtrl.TempData["Success"]);

        var savedAsset = await db.Assets.SingleAsync(x => x.Code == "AST-CTRL");

        // Edit 與 Delete 視圖正常載入
        Assert.IsType<ViewResult>(await controller.Index("assets"));
        Assert.IsType<ViewResult>(await controller.Edit("assets", savedAsset.Id));
        Assert.IsType<ViewResult>(await controller.Delete("assets", savedAsset.Id));
        Assert.IsType<NotFoundResult>(await controller.Delete("assets", 99999));

        // 確認刪除成功
        var delRedirect = Assert.IsType<RedirectToActionResult>(
            await controller.ConfirmDelete("assets", savedAsset.Id, savedAsset.Version));
        Assert.Equal("Index", delRedirect.ActionName);
        Assert.Equal("資產已刪除。", controller.TempData["Success"]);
        Assert.Empty(db.Assets);
    }

    [Fact]
    public async Task ManageController_Generate_CreatesMonthlyRentInvoicesIdempotentlyAndProrates()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        db.Assets.Add(new Asset { Id = 1, Code = "AST-01", Name = "大樓", Status = "啟用" });
        db.Units.Add(new Unit { Id = 2, Code = "UNT-01", Name = "1F", AssetId = 1, Status = "啟用" });
        db.Tenants.Add(new Tenant { Id = 3, Code = "TNT-01", Name = "租戶", Status = "啟用" });
        db.Leases.Add(new Lease
        {
            Id = 4,
            Code = "LSE-01",
            Name = "半月起租合約",
            UnitId = 2,
            TenantId = 3,
            Status = "有效",
            StartDate = new DateTime(2026, 1, 16),
            EndDate = new DateTime(2026, 12, 31),
            Rent = 31000m,
            DueDay = 5
        });
        await db.SaveChangesAsync();

        var controller = CreateManageController(db);

        // 非 invoices 模組回傳 404
        Assert.IsType<NotFoundResult>(await controller.Generate("assets", "2026-01"));

        // 非法月份格式回傳錯誤訊息
        await controller.Generate("invoices", "invalid-month");
        Assert.Equal("請選擇正確的計費月份。", controller.TempData["Error"]);

        // 首次產生 2026-01 租金應收（1/16~1/31 共 16 天 -> 16,000 元）
        await controller.Generate("invoices", "2026-01");
        Assert.Equal("已產生 1 筆租金應收，已存在的帳款自動略過。", controller.TempData["Success"]);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(16000m, invoice.Amount);
        Assert.Equal(new DateTime(2026, 1, 5), invoice.DueDate);

        // 重複執行同月份不會建立重複帳款
        await controller.Generate("invoices", "2026-01");
        Assert.Equal("已產生 0 筆租金應收，已存在的帳款自動略過。", controller.TempData["Success"]);
        Assert.Equal(1, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task ManageController_Generate_CreatesMonthlyEInvoicesIdempotentlyForB2BAndB2C()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;
        var period = new DateTime(2026, 2, 1);

        db.Assets.Add(new Asset { Id = 1, Code = "AST-01", Name = "大樓", Status = "啟用" });
        db.Units.AddRange(
            new Unit { Id = 2, Code = "UNT-01", Name = "1F", AssetId = 1, Status = "啟用" },
            new Unit { Id = 3, Code = "UNT-02", Name = "2F", AssetId = 1, Status = "啟用" }
        );
        db.Tenants.AddRange(
            new Tenant { Id = 4, Code = "TNT-01", Name = "企業租戶", Kind = "公司", TaxId = "24536806", Status = "啟用" },
            new Tenant { Id = 5, Code = "TNT-02", Name = "個人租戶", Kind = "個人", TaxId = "", Status = "啟用" }
        );
        db.Leases.AddRange(
            new Lease { Id = 6, Code = "LSE-01", Name = "企業合約", UnitId = 2, TenantId = 4, Status = "有效", Rent = 31500m },
            new Lease { Id = 7, Code = "LSE-02", Name = "個人合約", UnitId = 3, TenantId = 5, Status = "有效", Rent = 21000m }
        );
        db.Invoices.AddRange(
            new Invoice { Id = 8, Code = "INV-01", Name = "2月企業租金", LeaseId = 6, Kind = "租金", Period = period, Amount = 31500m, Status = "啟用" },
            new Invoice { Id = 9, Code = "INV-02", Name = "2月個人租金", LeaseId = 7, Kind = "租金", Period = period, Amount = 21000m, Status = "啟用" }
        );
        await db.SaveChangesAsync();

        var controller = CreateManageController(db);

        // 首次批次開立 2026-02 電子發票
        await controller.Generate("einvoices", "2026-02");
        Assert.Equal("已開立 2 張電子發票，已開立的帳款自動略過。", controller.TempData["Success"]);

        var einvoices = await db.EInvoices.OrderBy(x => x.InvoiceId).ToListAsync();
        Assert.Equal(2, einvoices.Count);

        var b2b = einvoices[0];
        Assert.Equal("B2B 存證", b2b.Category);
        Assert.Equal("24536806", b2b.BuyerTaxId);
        Assert.Equal("無載具", b2b.CarrierType);
        Assert.Equal(30000m, b2b.SalesAmount);
        Assert.Equal(1500m, b2b.TaxAmount);
        Assert.Equal(31500m, b2b.Amount);
        Assert.NotEqual("", Business.NormalizeTrackNumber(b2b.TrackNumber));

        var b2c = einvoices[1];
        Assert.Equal("B2C 存證", b2c.Category);
        Assert.Equal("", b2c.BuyerTaxId);
        Assert.Equal("會員載具", b2c.CarrierType);
        Assert.Equal("TNT-02", b2c.CarrierId);
        Assert.Equal(20000m, b2c.SalesAmount);
        Assert.Equal(1000m, b2c.TaxAmount);
        Assert.Equal(21000m, b2c.Amount);

        // 重複執行同月份不會重複開立
        await controller.Generate("einvoices", "2026-02");
        Assert.Equal("已開立 0 張電子發票，已開立的帳款自動略過。", controller.TempData["Success"]);
        Assert.Equal(2, await db.EInvoices.CountAsync());
    }
}
