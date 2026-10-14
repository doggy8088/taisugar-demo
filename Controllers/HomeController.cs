using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;
using PropertyManagement.Models;
using PropertyManagement.Services;
namespace PropertyManagement.Controllers;
public class HomeController(AppDbContext db) : Controller {
 public async Task<IActionResult> Index(string? month) {
  var today=Business.Today;var selected=new DateTime(today.Year,today.Month,1);
  if(month!=null&&DateTime.TryParseExact(month,"yyyy-MM",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var parsed)&&parsed.Year>=1900&&parsed.Year<=2100)selected=parsed;
  var assets=await db.Assets.AsNoTracking().ToListAsync();var units=await db.Units.AsNoTracking().ToListAsync();
  var leases=await db.Leases.AsNoTracking().ToListAsync();var tenants=await db.Tenants.AsNoTracking().ToListAsync();
  var invoices=await db.Invoices.AsNoTracking().Where(x=>x.Status=="啟用").ToListAsync();
  var payments=await db.Payments.AsNoTracking().Where(x=>x.Status=="啟用").ToListAsync();
  var snapshot=selected.Year==today.Year&&selected.Month==today.Month?today:selected.AddMonths(1).AddDays(-1);
  var available=units.Where(x=>x.Status=="啟用"&&assets.Any(a=>a.Id==x.AssetId&&a.Status=="啟用")).ToList();
  var model=new DashboardModel{Month=selected,Assets=assets.Count,Units=available.Count,Occupied=available.Count(u=>leases.Any(l=>l.UnitId==u.Id&&l.Status=="有效"&&l.StartDate<=snapshot&&l.EndDate>=snapshot))};
  model.SceneSnapshot=snapshot;
  model.SceneAssets=PropertySceneService.Build(assets,units,tenants,leases,invoices,payments,snapshot,today);
  model.Expected=invoices.Where(x=>x.Period==selected&&x.Kind=="租金").Sum(x=>x.Amount);
  var rentIds=invoices.Where(x=>x.Kind=="租金").Select(x=>x.Id).ToHashSet();
  model.Received=payments.Where(x=>x.PaidDate>=selected&&x.PaidDate<selected.AddMonths(1)&&rentIds.Contains(x.InvoiceId)).Sum(x=>x.Amount);
  foreach(var invoice in invoices) {
   var balance=invoice.Amount-payments.Where(x=>x.InvoiceId==invoice.Id).Sum(x=>x.Amount);
   if(balance>0&&invoice.DueDate<today) {
    model.Outstanding+=balance;
    var lease=leases.First(x=>x.Id==invoice.LeaseId);
    model.Unpaid.Add((invoice,balance,tenants.First(x=>x.Id==lease.TenantId).Name));
   }
  }
  for(int m=-5;m<=0;m++) {
   var period=selected.AddMonths(m);
   model.Trends.Add(new(period.ToString("MM 月"),invoices.Where(x=>x.Period==period&&x.Kind=="租金").Sum(x=>x.Amount),payments.Where(x=>x.PaidDate>=period&&x.PaidDate<period.AddMonths(1)&&rentIds.Contains(x.InvoiceId)).Sum(x=>x.Amount)));
  }
  model.Expiring=leases.Where(x=>x.Status=="有效"&&x.EndDate>=today&&x.EndDate<=today.AddDays(90)).OrderBy(x=>x.EndDate).Take(5).Select(x=>(x,tenants.First(t=>t.Id==x.TenantId).Name,units.First(u=>u.Id==x.UnitId).Name)).ToList();
  model.Unpaid=model.Unpaid.OrderBy(x=>x.Invoice.DueDate).Take(5).ToList();
  model.Portfolio=assets.GroupBy(x=>x.Category).Select(x=>(x.Key,x.Count())).ToList();
  return View(model);
 }
 [ResponseCache(Duration=0,Location=ResponseCacheLocation.None,NoStore=true)]
 public IActionResult Error()=>View(new ErrorViewModel{RequestId=Activity.Current?.Id??HttpContext.TraceIdentifier});
}
