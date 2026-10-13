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
  if(module!="invoices")return NotFound();
  if(!DateTime.TryParseExact(month,"yyyy-MM",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var period)||period.Year<1900||period.Year>2100) {TempData["Error"]="請選擇正確的計費月份。";return RedirectToAction(nameof(Index),new{module});}
  await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  var leases=await db.Leases.Where(x=>x.Status=="有效"&&x.StartDate<period.AddMonths(1)&&x.EndDate>=period).ToListAsync();int count=0;
  foreach(var lease in leases) {
   if(await db.Invoices.AnyAsync(x=>x.LeaseId==lease.Id&&x.Period==period&&x.Kind=="租金"))continue;
   db.Invoices.Add(new Invoice{Id=System.Security.Cryptography.RandomNumberGenerator.GetInt32(1,int.MaxValue),Code=$"RENT-{period:yyyyMM}-{lease.Id}",Name=$"{period:yyyy 年 MM 月} {lease.Name}",LeaseId=lease.Id,Period=period,DueDate=period.AddDays(lease.DueDay-1),Amount=Business.MonthlyRent(lease,period)});count++;
  }
  try {await db.SaveChangesAsync();await transaction.CommitAsync();TempData["Success"]=$"已產生 {count} 筆租金應收，已存在的帳款自動略過。";}
  catch(DbUpdateException){TempData["Error"]="本期已有其他人出帳，請重新查看列表。";}
  return RedirectToAction(nameof(Index),new{module});
 }
}
