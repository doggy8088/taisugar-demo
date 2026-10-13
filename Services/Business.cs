using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;
using PropertyManagement.Models;
namespace PropertyManagement.Services;
public static class Business {
 public static DateTime Today=>TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")).Date;
 public static async Task<List<string>> Validate(AppDbContext db,Record record,Record? original) {
  List<string> errors=[];
  switch(record) {
   case Lease l:
    if(l.EndDate<l.StartDate)errors.Add("到期日不得早於起租日。");
    if(l.DueDay<1||l.DueDay>28)errors.Add("繳款日必須介於 1 至 28 日。");
    if(l.Rent<=0)errors.Add("租金必須大於零。");
    if(l.Status=="有效") {
     var unit=await db.Units.AsNoTracking().FirstAsync(x=>x.Id==l.UnitId);
     if(unit.Status!="啟用"||!await db.Assets.AnyAsync(x=>x.Id==unit.AssetId&&x.Status=="啟用"))errors.Add("僅可出租啟用中的資產及單位。");
     if(!await db.Tenants.AnyAsync(x=>x.Id==l.TenantId&&x.Status=="啟用"))errors.Add("承租戶必須為啟用狀態。");
     if(await db.Leases.AnyAsync(x=>x.Id!=l.Id&&x.UnitId==l.UnitId&&x.Status=="有效"&&x.StartDate<=l.EndDate&&x.EndDate>=l.StartDate))errors.Add("此出租單位在指定期間已有有效租約，租期不可重疊。");
    }
    if(original is Lease old&&await db.Invoices.AnyAsync(x=>x.LeaseId==l.Id)&&(old.UnitId!=l.UnitId||old.TenantId!=l.TenantId||old.Rent!=l.Rent||old.StartDate!=l.StartDate||old.EndDate!=l.EndDate||old.Deposit!=l.Deposit||old.DueDay!=l.DueDay))errors.Add("此租約已產生帳款，計費及承租條件不可直接改寫；續約請建立新租約。");
    break;
   case Tenant t:
    if(t.TaxId.Length>0&&(t.TaxId.Length!=8||!t.TaxId.All(char.IsAsciiDigit)))errors.Add("統一編號須為 8 位數字。");break;
   case Reading r:
    if(r.Current<r.Previous)errors.Add("本次讀數不得小於前次讀數；換表請建立新水電表。");
    r.Amount=Math.Round((r.Current-r.Previous)*r.Rate,0,MidpointRounding.AwayFromZero);
    if(await db.Readings.AnyAsync(x=>x.Id!=r.Id&&x.MeterId==r.MeterId&&x.ReadDate==r.ReadDate))errors.Add("同一水電表同一天已有抄表紀錄。");break;
   case Invoice i:
    i.Period=new DateTime(i.Period.Year,i.Period.Month,1);
    if(i.Amount<=0)errors.Add("應收金額必須大於零。");
    if(await db.Invoices.AnyAsync(x=>x.Id!=i.Id&&x.LeaseId==i.LeaseId&&x.Kind==i.Kind&&x.Period==i.Period))errors.Add("此租約同月份已有相同類型的帳款。");
    if(original is Invoice oi&&await db.Payments.AnyAsync(x=>x.InvoiceId==i.Id)&&(i.Amount!=oi.Amount||i.LeaseId!=oi.LeaseId||i.Kind!=oi.Kind||i.Period!=oi.Period||i.Status!=oi.Status))errors.Add("此帳款已有收款紀錄，不可更改金額、歸屬或作廢。");break;
   case Payment p:
    if(p.Amount<=0)errors.Add("收款金額必須大於零。");
    var invoice=await db.Invoices.AsNoTracking().FirstAsync(x=>x.Id==p.InvoiceId);
    if(p.Status=="啟用") {
     if(invoice.Status!="啟用")errors.Add("不可收取已作廢帳款。");
     var paid=await db.Payments.Where(x=>x.InvoiceId==p.InvoiceId&&x.Id!=p.Id&&x.Status=="啟用").Select(x=>x.Amount).ToListAsync();
     if(paid.Sum()+p.Amount>invoice.Amount)errors.Add($"收款超過未收餘額 NT$ {(invoice.Amount-paid.Sum()):N0}。");
    }
    if(original is Payment op&&(op.InvoiceId!=p.InvoiceId||op.Amount!=p.Amount||op.PaidDate!=p.PaidDate))errors.Add("已登錄收款不可改寫金額、帳款歸屬或日期；請作廢原紀錄再新增。");break;
   case Unit u:
    if(original is Unit ou&&ou.AssetId!=u.AssetId&&await db.Leases.AnyAsync(x=>x.UnitId==u.Id))errors.Add("已有租約的出租單位不可變更所屬資產。");break;
  }
  return errors;
 }
 public static decimal MonthlyRent(Lease lease,DateTime month) {
  var last=month.AddMonths(1).AddDays(-1);var start=lease.StartDate>month?lease.StartDate:month;var end=lease.EndDate<last?lease.EndDate:last;
  if(end<start)return 0;
  return Math.Round(lease.Rent*(decimal)((end-start).Days+1)/DateTime.DaysInMonth(month.Year,month.Month),0,MidpointRounding.AwayFromZero);
 }
}
