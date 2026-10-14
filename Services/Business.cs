using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;
using PropertyManagement.Models;
namespace PropertyManagement.Services;
public static class Business {
 public static DateTime Today=>TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")).Date;
 public static async Task<List<string>> Validate(AppDbContext db,Record record,Record? original) {
  List<string> errors=[];
  foreach(var property in record.GetType().GetProperties().Where(p=>p.PropertyType==typeof(decimal)&&p.Name is "Rent" or "Deposit" or "Amount"))
   if(property.GetValue(record) is decimal money && decimal.Truncate(money)!=money)errors.Add("租金、押金、應收與收款金額以整數元記錄。");
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
   case Asset a:
    if(a.Status!="啟用" && await db.Leases.AnyAsync(l=>l.Status=="有效"&&l.EndDate>=Today&&db.Units.Any(u=>u.Id==l.UnitId&&u.AssetId==a.Id)))errors.Add("此資產仍有有效租約，請先終止相關租約再停用或轉維修。");
    break;
   case Meter m:
    if(original is Meter om&&om.UnitId!=m.UnitId&&await db.Readings.AnyAsync(x=>x.MeterId==m.Id))errors.Add("已有抄表紀錄的水電表不可更換所屬單位；請停用並建立新表。");
    break;
   case Reading r:
    if(r.Current<r.Previous)errors.Add("本次讀數不得小於前次讀數；換表請建立新水電表。");
    r.Amount=Math.Round((r.Current-r.Previous)*r.Rate,0,MidpointRounding.AwayFromZero);
    if(r.Amount>999999999m)errors.Add("計算後費用超過金額上限 999,999,999 元。");
    if(await db.Readings.AnyAsync(x=>x.Id!=r.Id&&x.MeterId==r.MeterId&&x.ReadDate==r.ReadDate))errors.Add("同一水電表同一天已有抄表紀錄。");break;
   case Invoice i:
    i.Period=new DateTime(i.Period.Year,i.Period.Month,1);
    var invoiceLease=await db.Leases.AsNoTracking().FirstAsync(x=>x.Id==i.LeaseId);
    if(invoiceLease.Status=="草稿")errors.Add("草稿租約不可建立帳款。");
    if(i.Kind=="租金"&&(i.Period>invoiceLease.EndDate||i.Period.AddMonths(1)<=invoiceLease.StartDate))errors.Add("租金月份必須與租約期間重疊。");
    if(i.Amount<=0)errors.Add("應收金額必須大於零。");
    if(await db.Invoices.AnyAsync(x=>x.Id!=i.Id&&x.LeaseId==i.LeaseId&&x.Kind==i.Kind&&x.Period==i.Period))errors.Add("此租約同月份已有相同類型的帳款。");
    if(original is Invoice oi&&await db.Payments.AnyAsync(x=>x.InvoiceId==i.Id)&&(i.Amount!=oi.Amount||i.LeaseId!=oi.LeaseId||i.Kind!=oi.Kind||i.Period!=oi.Period||i.Status!=oi.Status))errors.Add("此帳款已有收款紀錄，不可更改金額、歸屬或作廢。");
    if(original is Invoice oie&&await db.EInvoices.AnyAsync(x=>x.InvoiceId==i.Id&&x.Status=="啟用")&&(i.Amount!=oie.Amount||i.LeaseId!=oie.LeaseId||i.Kind!=oie.Kind||i.Period!=oie.Period||i.Status!=oie.Status))errors.Add("此帳款已有有效電子發票，請先作廢電子發票再更改或作廢帳款。");break;
   case Payment p:
    if(p.PaidDate>Today)errors.Add("收款日期不可晚於今天。");
    if(p.Amount<=0)errors.Add("收款金額必須大於零。");
    var invoice=await db.Invoices.AsNoTracking().FirstAsync(x=>x.Id==p.InvoiceId);
    if(p.Status=="啟用") {
     if(invoice.Status!="啟用")errors.Add("不可收取已作廢帳款。");
     var paid=await db.Payments.Where(x=>x.InvoiceId==p.InvoiceId&&x.Id!=p.Id&&x.Status=="啟用").Select(x=>x.Amount).ToListAsync();
     if(paid.Sum()+p.Amount>invoice.Amount)errors.Add($"收款超過未收餘額 NT$ {(invoice.Amount-paid.Sum()):N0}。");
    }
    if(original is Payment op&&(op.InvoiceId!=p.InvoiceId||op.Amount!=p.Amount||op.PaidDate!=p.PaidDate))errors.Add("已登錄收款不可改寫金額、帳款歸屬或日期；請作廢原紀錄再新增。");break;
   case EInvoice e:
    var track=NormalizeTrackNumber(e.TrackNumber);
    if(track.Length==0)errors.Add("發票字軌號碼格式須為 2 碼大寫英文加 8 位數字（如 AB-12345678）。");
    else {
     e.TrackNumber=track;
     if(await db.EInvoices.AnyAsync(x=>x.Id!=e.Id&&x.TrackNumber==e.TrackNumber))errors.Add("此發票字軌號碼已被使用。");
    }
    if(e.IssuedDate>Today)errors.Add("開立日期不可晚於今天。");
    if(e.Amount<=0)errors.Add("發票總額必須大於零。");
    (e.SalesAmount,e.TaxAmount)=CalculateTax(e.Amount,e.TaxType);
    if(e.RandomCode.Length!=4||!e.RandomCode.All(char.IsAsciiDigit))errors.Add("四碼隨機碼須為 4 位數字。");
    if(e.BuyerTaxId.Length>0&&(e.BuyerTaxId.Length!=8||!e.BuyerTaxId.All(char.IsAsciiDigit)))errors.Add("買方統一編號須為 8 位數字。");
    if(e.Category=="B2B 存證"&&e.BuyerTaxId.Length!=8)errors.Add("B2B 存證發票必須填寫 8 位數買方統一編號。");
    if(e.Category=="B2C 存證"&&e.BuyerTaxId.Length>0)errors.Add("B2C 存證發票不可填寫買方統一編號；企業統編請選擇 B2B 存證。");
    if(e.Category=="B2B 存證"&&(e.CarrierType!="無載具"||e.CarrierId.Length>0||e.DonateCode.Length>0))errors.Add("B2B 存證發票不可使用雲端載具或愛心碼捐贈。");
    if(e.CarrierType=="無載具"&&e.CarrierId.Length>0)errors.Add("選擇無載具時請勿填寫載具號碼。");
    if(e.CarrierType=="手機條碼"&&(e.CarrierId.Length!=8||e.CarrierId[0]!='/'||!e.CarrierId[1..].All(c=>char.IsAsciiLetterUpper(c)||char.IsAsciiDigit(c)||c is '.' or '+' or '-')))errors.Add("手機條碼格式須為 / 開頭加 7 碼大寫英數或符號（.+ -）。");
    if(e.CarrierType=="自然人憑證"&&(e.CarrierId.Length!=16||!e.CarrierId[..2].All(char.IsAsciiLetterUpper)||!e.CarrierId[2..].All(char.IsAsciiDigit)))errors.Add("自然人憑證條碼須為 2 碼大寫英文加 14 位數字。");
    if(e.CarrierType=="會員載具"&&e.CarrierId.Length==0)errors.Add("請填寫會員載具號碼。");
    if(e.DonateCode.Length>0&&(e.DonateCode.Length<3||e.DonateCode.Length>7||!e.DonateCode.All(char.IsAsciiDigit)))errors.Add("愛心碼須為 3 至 7 位數字。");
    var targetInvoice=await db.Invoices.AsNoTracking().FirstAsync(x=>x.Id==e.InvoiceId);
    if(e.Status=="啟用") {
     if(targetInvoice.Status!="啟用")errors.Add("不可對已作廢帳款開立發票。");
     var issued=await db.EInvoices.Where(x=>x.InvoiceId==e.InvoiceId&&x.Id!=e.Id&&x.Status=="啟用").Select(x=>x.Amount).ToListAsync();
     if(issued.Sum()+e.Amount>targetInvoice.Amount)errors.Add($"發票總額超過應收帳款可開立餘額 NT$ {(targetInvoice.Amount-issued.Sum()):N0}。");
    }
    if(original is EInvoice oe&&(oe.InvoiceId!=e.InvoiceId||oe.TrackNumber!=e.TrackNumber||oe.Amount!=e.Amount||oe.BuyerTaxId!=e.BuyerTaxId||oe.IssuedDate!=e.IssuedDate||oe.Category!=e.Category||oe.TaxType!=e.TaxType))errors.Add("已開立發票不可改寫字軌號碼、金額、帳款歸屬、統編或開立日期；請作廢原發票再重新開立。");break;
   case Unit u:
    if(u.Status!="啟用"&&await db.Leases.AnyAsync(x=>x.UnitId==u.Id&&x.Status=="有效"&&x.EndDate>=Today))errors.Add("此單位仍有有效租約，請先終止租約再變更使用狀態。");
    if(original is Unit ou&&ou.AssetId!=u.AssetId&&await db.Leases.AnyAsync(x=>x.UnitId==u.Id))errors.Add("已有租約的出租單位不可變更所屬資產。");break;
  }
  return errors;
 }
 public static decimal MonthlyRent(Lease lease,DateTime month) {
  var last=month.AddMonths(1).AddDays(-1);var start=lease.StartDate>month?lease.StartDate:month;var end=lease.EndDate<last?lease.EndDate:last;
  if(end<start)return 0;
  return Math.Round(lease.Rent*(decimal)((end-start).Days+1)/DateTime.DaysInMonth(month.Year,month.Month),0,MidpointRounding.AwayFromZero);
 }
 public static string NormalizeTrackNumber(string? raw) {
  var s=(raw??"").Trim().ToUpperInvariant();
  if(s.Length==11&&s[2]=='-')s=s[..2]+s[3..];
  return s.Length==10&&s[..2].All(char.IsAsciiLetterUpper)&&s[2..].All(char.IsAsciiDigit)?$"{s[..2]}-{s[2..]}":"";
 }
 public static (decimal Sales,decimal Tax) CalculateTax(decimal totalAmount,string taxType) {
  if(taxType!="應稅")return (totalAmount,0m);
  var sales=Math.Round(totalAmount/1.05m,0,MidpointRounding.AwayFromZero);
  return (sales,totalAmount-sales);
 }
}
