using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;
using PropertyManagement.Models;
using PropertyManagement.Services;
namespace PropertyManagement.Controllers;
[Route("manage/{module}")]
public class ManageController(AppDbContext db) : Controller {
 IModuleService Service(Module module)=>(IModuleService)Activator.CreateInstance(typeof(ModuleService<>).MakeGenericType(module.Type),db,module)!;
 [HttpGet("")]
 public async Task<IActionResult> Index(string module,string q="",string status="",int page=1,int size=10,string sort="code") {
  var definition=Catalog.Find(module);if(definition==null)return NotFound();
  return View(await Service(definition).List(q,status,page,size,sort));
 }
 [HttpGet("create")][HttpGet("edit/{id:int}")]
 public async Task<IActionResult> Edit(string module,int id=0) {
  var definition=Catalog.Find(module);if(definition==null)return NotFound();
  var model=await Service(definition).Edit(id);return model==null?NotFound():View(model);
 }
 [HttpPost("save/{id:int?}")]
 public async Task<IActionResult> Save(string module,int id=0) {
  var definition=Catalog.Find(module);if(definition==null)return NotFound();
  var result=await Service(definition).Save(id,await Request.ReadFormAsync());
  if(result.Errors.Count>0){Response.StatusCode=422;return View("Edit",result);}
  TempData["Success"]=$"{definition.Singular}已儲存。";return RedirectToAction(nameof(Index),new{module});
 }
 [HttpGet("delete/{id:int}")]
 public async Task<IActionResult> Delete(string module,int id) {
  var definition=Catalog.Find(module);if(definition==null)return NotFound();
  var model=await Service(definition).Edit(id);return model==null?NotFound():View(model);
 }
 [HttpPost("delete/{id:int}")][ActionName("Delete")]
 public async Task<IActionResult> ConfirmDelete(string module,int id,string version) {
  var definition=Catalog.Find(module);if(definition==null)return NotFound();
  var error=await Service(definition).Delete(id,version);TempData[error==null?"Success":"Error"]=error??$"{definition.Singular}已刪除。";
  return RedirectToAction(nameof(Index),new{module});
 }
 [HttpPost("generate")]
 public async Task<IActionResult> Generate(string module,string month) {
  if(module is not ("invoices" or "einvoices"))return NotFound();
  if(!DateTime.TryParseExact(month,"yyyy-MM",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var period)||period.Year<1900||period.Year>2100) {TempData["Error"]="請選擇正確的計費月份。";return RedirectToAction(nameof(Index),new{module});}
  await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  if(module=="invoices") {
   var leases=await db.Leases.Where(x=>x.Status=="有效"&&x.StartDate<period.AddMonths(1)&&x.EndDate>=period).ToListAsync();int count=0;
   foreach(var lease in leases) {
    if(await db.Invoices.AnyAsync(x=>x.LeaseId==lease.Id&&x.Period==period&&x.Kind=="租金"))continue;
    db.Invoices.Add(new Invoice{Id=System.Security.Cryptography.RandomNumberGenerator.GetInt32(1,int.MaxValue),Code=$"RENT-{period:yyyyMM}-{lease.Id}",Name=$"{period:yyyy 年 MM 月} {lease.Name}",LeaseId=lease.Id,Period=period,DueDate=period.AddDays(lease.DueDay-1),Amount=Business.MonthlyRent(lease,period)});count++;
   }
   try {await db.SaveChangesAsync();await transaction.CommitAsync();TempData["Success"]=$"已產生 {count} 筆租金應收，已存在的帳款自動略過。";}
   catch(DbUpdateException){TempData["Error"]="本期已有其他人出帳，請重新查看列表。";}
   return RedirectToAction(nameof(Index),new{module});
  }
  var invoices=await db.Invoices.Where(x=>x.Status=="啟用"&&x.Period==period).ToListAsync();
  var leaseMap=await db.Leases.AsNoTracking().ToDictionaryAsync(x=>x.Id);
  var tenantMap=await db.Tenants.AsNoTracking().ToDictionaryAsync(x=>x.Id);
  var usedTracks=(await db.EInvoices.AsNoTracking().Select(x=>x.TrackNumber).ToListAsync()).ToHashSet();
  string[] prefixes=["AB","CD","EF","GH","JK","LM"]; string prefix=prefixes[(period.Month-1)/2];
  int issuedCount=0;
  foreach(var inv in invoices) {
   if(await db.EInvoices.AnyAsync(x=>x.InvoiceId==inv.Id&&x.Status=="啟用"))continue;
   if(!leaseMap.TryGetValue(inv.LeaseId,out var lease)||!tenantMap.TryGetValue(lease.TenantId,out var tenant))continue;
   int seq=20000000+(inv.Id%70000000);
   string track;
   do { track=$"{prefix}-{seq++:00000000}"; } while(!usedTracks.Add(track));
   bool b2b=tenant.TaxId.Length==8&&tenant.TaxId.All(char.IsAsciiDigit);
   var (sales,tax)=Business.CalculateTax(inv.Amount,"應稅");
   db.EInvoices.Add(new EInvoice{
    Id=System.Security.Cryptography.RandomNumberGenerator.GetInt32(1,int.MaxValue),
    Code=$"EINV-{period:yyyyMM}-{inv.Id}",
    Name=$"{inv.Name} 電子發票",
    InvoiceId=inv.Id,
    TrackNumber=track,
    IssuedDate=period<=Business.Today?period:Business.Today,
    Category=b2b?"B2B 存證":"B2C 存證",
    TaxType="應稅",
    BuyerTaxId=b2b?tenant.TaxId:"",
    CarrierType=b2b?"無載具":"會員載具",
    CarrierId=b2b?"":tenant.Code,
    DonateCode="",
    RandomCode=System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000,10000).ToString("0000"),
    SalesAmount=sales,
    TaxAmount=tax,
    Amount=inv.Amount,
    Status="啟用"
   });
   issuedCount++;
  }
  try {await db.SaveChangesAsync();await transaction.CommitAsync();TempData["Success"]=$"已開立 {issuedCount} 張電子發票，已開立的帳款自動略過。";}
  catch(DbUpdateException){TempData["Error"]="本期已有其他人開立發票，請重新查看列表。";}
  return RedirectToAction(nameof(Index),new{module});
 }
}
