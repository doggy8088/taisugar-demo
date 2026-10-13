using PropertyManagement.Models;
using PropertyManagement.Services;
namespace PropertyManagement.Data;
public static class SeedData {
 public static void Initialize(AppDbContext db) {
  if(db.Assets.Any())return;
  int id=1;
  string[] names=["信義商務中心","民生創意大樓","南港企業總部","中山光寓","內湖科技廣場","板橋遠景商辦"];
  string[] addresses=["臺北市信義區松仁路 100 號","臺北市松山區民生東路三段 88 號","臺北市南港區經貿二路 66 號","臺北市中山區中山北路二段 120 號","臺北市內湖區瑞光路 258 號","新北市板橋區縣民大道二段 88 號"];
  var assets=names.Select((name,i)=>new Asset{Id=id++,Code=$"AST-{i+1:000}",Name=name,Address=addresses[i],Company=i<4?"禾境資產管理":"禾境資產管理・北區",Manager=i%2==0?"林怡君":"陳柏宇",Category=i==3?"住宅":"商辦",Area=320+i*80}).ToList();
  db.Assets.AddRange(assets);
  var units=Enumerable.Range(0,32).Select(i=>new Unit{Id=id++,Code=$"UNT-{i+1:000}",Name=$"{names[i%6]}・{i/6+2} 樓 {i%2+1} 室",AssetId=assets[i%6].Id,Floor=$"{i/6+2}F-{i%2+1}",Area=22+i%7*6,Status=i==30?"維修中":"啟用"}).ToList();db.Units.AddRange(units);
  string[] companies=["澄光設計有限公司","森野生活股份有限公司","沐序科技有限公司","青禾顧問有限公司","日常建築事務所","和光數位有限公司","島嶼製作有限公司","知行行銷股份有限公司","初見選品有限公司","遠見工程有限公司","林子晴","陳品安","暮光影像有限公司","有方創意有限公司","千里科技有限公司","豐盛國際有限公司","安禾有限公司","陸拾設計工作室","藍海策略有限公司","葉書豪","木木生活有限公司"];
  var tenants=companies.Select((name,i)=>new Tenant{Id=id++,Code=$"TNT-{i+1:000}",Name=name,Kind=i is 10 or 11 or 19?"個人":"公司",Contact=i%2==0?"林怡君":"陳柏宇",Phone=$"02-27{100000+i:000000}",Email=$"contact{i+1}@example.com",Address=addresses[i%6]}).ToList();db.Tenants.AddRange(tenants);
  var today=Business.Today;var month=new DateTime(today.Year,today.Month,1);
  var leases=tenants.Select((tenant,i)=>new Lease{Id=id++,Code=$"LSE-{i+1:000}",Name=$"{tenant.Name} 租賃合約",UnitId=units[i].Id,TenantId=tenant.Id,Status="有效",StartDate=month.AddMonths(-8-i%4),EndDate=month.AddMonths(i<3?1:12).AddDays(i<3?i*8: -1),Rent=26000+i*2500,Deposit=(26000+i*2500)*2,DueDay=5}).ToList();db.Leases.AddRange(leases);
  var meters=units.Take(21).Select((unit,i)=>new Meter{Id=id++,Code=$"MTR-{i+1:000}",Name=$"{unit.Name} 電表",UnitId=unit.Id,Kind="電表",Account=$"10-22-{i+1000}",Rate=5}).ToList();db.Meters.AddRange(meters);
  foreach(var meter in meters)db.Readings.Add(new Reading{Id=id++,Code=$"RDG-{meter.Id:000}",Name=$"{month:MM} 月用電紀錄",MeterId=meter.Id,ReadDate=month,Previous=1200,Current=1400+meter.Id%100,Rate=5,Amount=(200+meter.Id%100)*5});
  for(int m=-5;m<=0;m++)foreach(var lease in leases) {
   var period=month.AddMonths(m);var invoice=new Invoice{Id=id++,Code=$"INV-{period:yyyyMM}-{lease.Id}",Name=$"{period:MM} 月租金・{tenants.First(t=>t.Id==lease.TenantId).Name}",LeaseId=lease.Id,Period=period,DueDate=period.AddDays(4),Amount=lease.Rent};db.Invoices.Add(invoice);
   if(m<0||lease.Id%5!=0)db.Payments.Add(new Payment{Id=id++,Code=$"PAY-{invoice.Id}",Name=invoice.Name,InvoiceId=invoice.Id,PaidDate=period.AddDays(m<0?3:1),Amount=lease.Rent,Reference=$"DEMO{invoice.Id}"});
  }
  db.SaveChanges();
 }
}
