using PropertyManagement.Models;
using PropertyManagement.Services;

namespace PropertyManagement.Tests;

public class BusinessTests
{
    [Fact]
    public void MonthlyRent_FullMonth_ReturnsFullRent()
    {
        var lease = new Lease
        {
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Rent = 31000m
        };

        var amount = Business.MonthlyRent(lease, new DateTime(2026, 1, 1));

        Assert.Equal(31000m, amount);
    }

    [Fact]
    public void MonthlyRent_ProratedFirstMonth_CalculatesDaysInclusivelyAndRoundsAwayFromZero()
    {
        var lease = new Lease
        {
            StartDate = new DateTime(2026, 1, 16),
            EndDate = new DateTime(2026, 12, 31),
            Rent = 31000m
        };

        // 1/16 ~ 1/31 共 16 天，31000 * 16 / 31 = 16000
        var amount = Business.MonthlyRent(lease, new DateTime(2026, 1, 1));

        Assert.Equal(16000m, amount);
    }

    [Fact]
    public void MonthlyRent_ProratedLastMonth_CalculatesCorrectly()
    {
        var lease = new Lease
        {
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 4, 15),
            Rent = 30000m
        };

        // 4/1 ~ 4/15 共 15 天，4 月共 30 天，30000 * 15 / 30 = 15000
        var amount = Business.MonthlyRent(lease, new DateTime(2026, 4, 1));

        Assert.Equal(15000m, amount);
    }

    [Fact]
    public void MonthlyRent_MidpointRounding_RoundsAwayFromZero()
    {
        var lease = new Lease
        {
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2026, 4, 15),
            Rent = 10001m
        };

        // 10001 * 15 / 30 = 5000.5 -> AwayFromZero = 5001
        var amount = Business.MonthlyRent(lease, new DateTime(2026, 4, 1));

        Assert.Equal(5001m, amount);
    }

    [Fact]
    public void MonthlyRent_OutsideLeasePeriod_ReturnsZero()
    {
        var lease = new Lease
        {
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 5, 31),
            Rent = 25000m
        };

        Assert.Equal(0m, Business.MonthlyRent(lease, new DateTime(2026, 2, 1)));
        Assert.Equal(0m, Business.MonthlyRent(lease, new DateTime(2026, 6, 1)));
    }

    [Fact]
    public async Task Validate_NonIntegerMoneyFields_ReturnsError()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var lease = new Lease
        {
            Id = 1,
            Code = "LSE-01",
            Name = "測試租約",
            Status = "草稿",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Rent = 25000.5m,
            Deposit = 50000m,
            DueDay = 5
        };

        var errors = await Business.Validate(db, lease, null);

        Assert.Contains("租金、押金、應收與收款金額以整數元記錄。", errors);
    }

    [Fact]
    public async Task Validate_LeaseBasicConstraints_ChecksDatesDueDayAndPositiveRent()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var lease = new Lease
        {
            Id = 1,
            Code = "LSE-01",
            Name = "測試租約",
            Status = "草稿",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 5, 1),
            Rent = 0m,
            Deposit = 10000m,
            DueDay = 29
        };

        var errors = await Business.Validate(db, lease, null);

        Assert.Contains("到期日不得早於起租日。", errors);
        Assert.Contains("繳款日必須介於 1 至 28 日。", errors);
        Assert.Contains("租金必須大於零。", errors);
    }

    [Fact]
    public async Task Validate_ActiveLease_RequiresActiveAssetUnitTenantAndNoOverlap()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var asset = new Asset { Id = 1, Code = "AST-01", Name = "資產一", Status = "維修中" };
        var unit = new Unit { Id = 2, Code = "UNT-01", Name = "單位一", AssetId = 1, Status = "啟用" };
        var tenant = new Tenant { Id = 3, Code = "TNT-01", Name = "承租人一", Status = "停用" };
        var existingLease = new Lease
        {
            Id = 4,
            Code = "LSE-01",
            Name = "既有租約",
            UnitId = 2,
            TenantId = 3,
            Status = "有效",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 6, 30),
            Rent = 20000m,
            DueDay = 5
        };
        db.Assets.Add(asset);
        db.Units.Add(unit);
        db.Tenants.Add(tenant);
        db.Leases.Add(existingLease);
        await db.SaveChangesAsync();

        var newLease = new Lease
        {
            Id = 5,
            Code = "LSE-02",
            Name = "重疊租約",
            UnitId = 2,
            TenantId = 3,
            Status = "有效",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 12, 31),
            Rent = 22000m,
            DueDay = 5
        };

        var errors = await Business.Validate(db, newLease, null);

        Assert.Contains("僅可出租啟用中的資產及單位。", errors);
        Assert.Contains("承租戶必須為啟用狀態。", errors);
        Assert.Contains("此出租單位在指定期間已有有效租約，租期不可重疊。", errors);
    }

    [Fact]
    public async Task Validate_LeaseWithInvoices_BlocksTermChangesButAllowsTermination()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var asset = new Asset { Id = 1, Code = "AST-01", Name = "資產一", Status = "啟用" };
        var unit = new Unit { Id = 2, Code = "UNT-01", Name = "單位一", AssetId = 1, Status = "啟用" };
        var tenant = new Tenant { Id = 3, Code = "TNT-01", Name = "承租人一", Status = "啟用" };
        var original = new Lease
        {
            Id = 4,
            Code = "LSE-01",
            Name = "既有租約",
            UnitId = 2,
            TenantId = 3,
            Status = "有效",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Rent = 20000m,
            Deposit = 40000m,
            DueDay = 5
        };
        var invoice = new Invoice
        {
            Id = 5,
            Code = "INV-01",
            Name = "1月租金",
            LeaseId = 4,
            Kind = "租金",
            Period = new DateTime(2026, 1, 1),
            DueDate = new DateTime(2026, 1, 5),
            Amount = 20000m,
            Status = "啟用"
        };
        db.Assets.Add(asset);
        db.Units.Add(unit);
        db.Tenants.Add(tenant);
        db.Leases.Add(original);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var modifiedRent = new Lease
        {
            Id = 4,
            Code = "LSE-01",
            Name = "既有租約",
            UnitId = 2,
            TenantId = 3,
            Status = "有效",
            StartDate = original.StartDate,
            EndDate = original.EndDate,
            Rent = 25000m,
            Deposit = original.Deposit,
            DueDay = original.DueDay
        };
        var rentErrors = await Business.Validate(db, modifiedRent, original);
        Assert.Contains("此租約已產生帳款，計費及承租條件不可直接改寫；續約請建立新租約。", rentErrors);

        var terminated = new Lease
        {
            Id = 4,
            Code = "LSE-01",
            Name = "既有租約",
            UnitId = 2,
            TenantId = 3,
            Status = "已終止",
            StartDate = original.StartDate,
            EndDate = original.EndDate,
            Rent = original.Rent,
            Deposit = original.Deposit,
            DueDay = original.DueDay
        };
        var terminateErrors = await Business.Validate(db, terminated, original);
        Assert.Empty(terminateErrors);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("12345678", true)]
    [InlineData("1234567", false)]
    [InlineData("123456789", false)]
    [InlineData("1234567A", false)]
    public async Task Validate_TenantTaxId_EnforcesEightAsciiDigitsWhenProvided(string taxId, bool expectedValid)
    {
        using var scope = new TestDbScope();
        var tenant = new Tenant { Id = 1, Code = "TNT-01", Name = "測試戶", TaxId = taxId };

        var errors = await Business.Validate(scope.Context, tenant, null);

        if (expectedValid)
            Assert.Empty(errors);
        else
            Assert.Contains("統一編號須為 8 位數字。", errors);
    }

    [Fact]
    public async Task Validate_AssetAndUnitStatusChange_BlockedWhenActiveLeaseExists()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;
        var today = Business.Today;

        var asset1 = new Asset { Id = 1, Code = "AST-01", Name = "資產一", Status = "啟用" };
        var asset2 = new Asset { Id = 2, Code = "AST-02", Name = "資產二", Status = "啟用" };
        var unit = new Unit { Id = 3, Code = "UNT-01", Name = "單位一", AssetId = 1, Status = "啟用" };
        var tenant = new Tenant { Id = 4, Code = "TNT-01", Name = "租戶一", Status = "啟用" };
        var lease = new Lease
        {
            Id = 5,
            Code = "LSE-01",
            Name = "租約一",
            UnitId = 3,
            TenantId = 4,
            Status = "有效",
            StartDate = today.AddMonths(-1),
            EndDate = today.AddMonths(3),
            Rent = 20000m,
            DueDay = 5
        };
        db.Assets.AddRange(asset1, asset2);
        db.Units.Add(unit);
        db.Tenants.Add(tenant);
        db.Leases.Add(lease);
        await db.SaveChangesAsync();

        var disabledAsset = new Asset { Id = 1, Code = "AST-01", Name = "資產一", Status = "停用" };
        var assetErrors = await Business.Validate(db, disabledAsset, asset1);
        Assert.Contains("此資產仍有有效租約，請先終止相關租約再停用或轉維修。", assetErrors);

        var movedAndDisabledUnit = new Unit { Id = 3, Code = "UNT-01", Name = "單位一", AssetId = 2, Status = "維修中" };
        var unitErrors = await Business.Validate(db, movedAndDisabledUnit, unit);
        Assert.Contains("此單位仍有有效租約，請先終止租約再變更使用狀態。", unitErrors);
        Assert.Contains("已有租約的出租單位不可變更所屬資產。", unitErrors);
    }

    [Fact]
    public async Task Validate_MeterAndReading_EnforcesUnitLockCalculationAndDuplicateCheck()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;

        var asset = new Asset { Id = 1, Code = "AST-01", Name = "資產一", Status = "啟用" };
        var unit1 = new Unit { Id = 2, Code = "UNT-01", Name = "單位一", AssetId = 1, Status = "啟用" };
        var unit2 = new Unit { Id = 3, Code = "UNT-02", Name = "單位二", AssetId = 1, Status = "啟用" };
        var meter = new Meter { Id = 4, Code = "MTR-01", Name = "電表一", UnitId = 2, Rate = 5.5m };
        var existingReading = new Reading
        {
            Id = 5,
            Code = "RDG-01",
            Name = "前期抄表",
            MeterId = 4,
            ReadDate = new DateTime(2026, 5, 1),
            Previous = 100m,
            Current = 110.5m,
            Rate = 5.5m,
            Amount = 58m
        };
        db.Assets.Add(asset);
        db.Units.AddRange(unit1, unit2);
        db.Meters.Add(meter);
        db.Readings.Add(existingReading);
        await db.SaveChangesAsync();

        var movedMeter = new Meter { Id = 4, Code = "MTR-01", Name = "電表一", UnitId = 3, Rate = 5.5m };
        var meterErrors = await Business.Validate(db, movedMeter, meter);
        Assert.Contains("已有抄表紀錄的水電表不可更換所屬單位；請停用並建立新表。", meterErrors);

        var validReading = new Reading
        {
            Id = 6,
            Code = "RDG-02",
            Name = "本期抄表",
            MeterId = 4,
            ReadDate = new DateTime(2026, 6, 1),
            Previous = 100m,
            Current = 110.5m,
            Rate = 5.5m
        };
        var validReadingErrors = await Business.Validate(db, validReading, null);
        Assert.Empty(validReadingErrors);
        // (110.5 - 100) * 5.5 = 57.75 -> 58
        Assert.Equal(58m, validReading.Amount);

        var backwardDuplicateReading = new Reading
        {
            Id = 7,
            Code = "RDG-03",
            Name = "錯誤抄表",
            MeterId = 4,
            ReadDate = new DateTime(2026, 5, 1),
            Previous = 200m,
            Current = 150m,
            Rate = 5m
        };
        var readingErrors = await Business.Validate(db, backwardDuplicateReading, null);
        Assert.Contains("本次讀數不得小於前次讀數；換表請建立新水電表。", readingErrors);
        Assert.Contains("同一水電表同一天已有抄表紀錄。", readingErrors);

        var overflowReading = new Reading
        {
            Id = 8,
            Code = "RDG-04",
            Name = "超額抄表",
            MeterId = 4,
            ReadDate = new DateTime(2026, 7, 1),
            Previous = 0m,
            Current = 200000000m,
            Rate = 10m
        };
        var overflowErrors = await Business.Validate(db, overflowReading, null);
        Assert.Contains("計算後費用超過金額上限 999,999,999 元。", overflowErrors);
    }

    [Fact]
    public async Task Validate_InvoiceAndPayment_EnforcesAccountingProtections()
    {
        using var scope = new TestDbScope();
        var db = scope.Context;
        var today = Business.Today;

        var asset = new Asset { Id = 1, Code = "AST-01", Name = "資產一", Status = "啟用" };
        var unit = new Unit { Id = 2, Code = "UNT-01", Name = "單位一", AssetId = 1, Status = "啟用" };
        var tenant = new Tenant { Id = 3, Code = "TNT-01", Name = "租戶一", Status = "啟用" };
        var draftLease = new Lease
        {
            Id = 4,
            Code = "LSE-DRAFT",
            Name = "草稿租約",
            UnitId = 2,
            TenantId = 3,
            Status = "草稿",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Rent = 20000m
        };
        var activeLease = new Lease
        {
            Id = 5,
            Code = "LSE-ACTIVE",
            Name = "有效租約",
            UnitId = 2,
            TenantId = 3,
            Status = "有效",
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 6, 30),
            Rent = 20000m
        };
        var invoice = new Invoice
        {
            Id = 6,
            Code = "INV-01",
            Name = "3月租金",
            LeaseId = 5,
            Kind = "租金",
            Period = new DateTime(2026, 3, 1),
            DueDate = new DateTime(2026, 3, 5),
            Amount = 20000m,
            Status = "啟用"
        };
        var voidInvoice = new Invoice
        {
            Id = 7,
            Code = "INV-VOID",
            Name = "作廢帳款",
            LeaseId = 5,
            Kind = "水費",
            Period = new DateTime(2026, 3, 1),
            DueDate = new DateTime(2026, 3, 5),
            Amount = 1000m,
            Status = "作廢"
        };
        var payment = new Payment
        {
            Id = 8,
            Code = "PAY-01",
            Name = "第一筆收款",
            InvoiceId = 6,
            PaidDate = today.AddDays(-1),
            Amount = 12000m,
            Status = "啟用"
        };
        db.Assets.Add(asset);
        db.Units.Add(unit);
        db.Tenants.Add(tenant);
        db.Leases.AddRange(draftLease, activeLease);
        db.Invoices.AddRange(invoice, voidInvoice);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        // 草稿租約與非租期月份檢查
        var draftInv = new Invoice
        {
            Id = 9,
            Code = "INV-02",
            Name = "草稿帳款",
            LeaseId = 4,
            Kind = "租金",
            Period = new DateTime(2026, 1, 15),
            Amount = 0m
        };
        var draftInvErrors = await Business.Validate(db, draftInv, null);
        Assert.Equal(new DateTime(2026, 1, 1), draftInv.Period);
        Assert.Contains("草稿租約不可建立帳款。", draftInvErrors);
        Assert.Contains("應收金額必須大於零。", draftInvErrors);

        var outOfRangeInv = new Invoice
        {
            Id = 10,
            Code = "INV-03",
            Name = "超期帳款",
            LeaseId = 5,
            Kind = "租金",
            Period = new DateTime(2026, 8, 1),
            Amount = 20000m
        };
        var outOfRangeErrors = await Business.Validate(db, outOfRangeInv, null);
        Assert.Contains("租金月份必須與租約期間重疊。", outOfRangeErrors);

        // 重複帳款與已收款帳款鎖定
        var duplicateInv = new Invoice
        {
            Id = 11,
            Code = "INV-DUP",
            Name = "重複帳款",
            LeaseId = 5,
            Kind = "租金",
            Period = new DateTime(2026, 3, 1),
            Amount = 20000m
        };
        Assert.Contains("此租約同月份已有相同類型的帳款。", await Business.Validate(db, duplicateInv, null));

        var voidPaidInvoice = new Invoice
        {
            Id = 6,
            Code = "INV-01",
            Name = "3月租金",
            LeaseId = 5,
            Kind = "租金",
            Period = new DateTime(2026, 3, 1),
            DueDate = new DateTime(2026, 3, 5),
            Amount = 20000m,
            Status = "作廢"
        };
        Assert.Contains("此帳款已有收款紀錄，不可更改金額、歸屬或作廢。", await Business.Validate(db, voidPaidInvoice, invoice));

        // 收款驗證：未來日期、超額收款、作廢帳款、竄改已登錄收款
        var overPay = new Payment
        {
            Id = 12,
            Code = "PAY-OVER",
            Name = "超額收款",
            InvoiceId = 6,
            PaidDate = today.AddDays(1),
            Amount = 9000m,
            Status = "啟用"
        };
        var overPayErrors = await Business.Validate(db, overPay, null);
        Assert.Contains("收款日期不可晚於今天。", overPayErrors);
        Assert.Contains("收款超過未收餘額 NT$ 8,000。", overPayErrors);

        var payVoidInv = new Payment
        {
            Id = 13,
            Code = "PAY-VOID-INV",
            Name = "收作廢帳",
            InvoiceId = 7,
            PaidDate = today,
            Amount = 500m,
            Status = "啟用"
        };
        Assert.Contains("不可收取已作廢帳款。", await Business.Validate(db, payVoidInv, null));

        var alteredPayment = new Payment
        {
            Id = 8,
            Code = "PAY-01",
            Name = "第一筆收款",
            InvoiceId = 6,
            PaidDate = payment.PaidDate,
            Amount = 10000m,
            Status = "啟用"
        };
        Assert.Contains("已登錄收款不可改寫金額、帳款歸屬或日期；請作廢原紀錄再新增。", await Business.Validate(db, alteredPayment, payment));

        // 允許將既有收款改為作廢
        var voidedPayment = new Payment
        {
            Id = 8,
            Code = "PAY-01",
            Name = "第一筆收款",
            InvoiceId = 6,
            PaidDate = payment.PaidDate,
            Amount = payment.Amount,
            Status = "作廢"
        };
        Assert.Empty(await Business.Validate(db, voidedPayment, payment));
    }
}
