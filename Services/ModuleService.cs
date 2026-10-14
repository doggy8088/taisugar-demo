using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;
using PropertyManagement.Models;
namespace PropertyManagement.Services;
public interface IModuleService {
 Task<ListModel> List(string q,string status,int page,int size,string sort);
 Task<EditModel?> Edit(int id);
 Task<EditModel> Save(int id,IFormCollection form);
 Task<string?> Delete(int id,string version);
}
public class ModuleService<T>(AppDbContext db,Module module) : IModuleService where T:Record,new() {
 public async Task<ListModel> List(string q,string status,int page,int size,string sort) {
  size=new[]{10,20,50}.Contains(size)?size:10;
  var query=db.Set<T>().AsNoTracking().AsQueryable(); q=q.Trim();
  if(q.Length>0) query=query.Where(x=>x.Code.Contains(q)||x.Name.Contains(q)||x.Notes.Contains(q));
  if(module.States.Contains(status)) query=query.Where(x=>x.Status==status); else status="";
  query=sort switch {"name"=>query.OrderBy(x=>x.Name).ThenBy(x=>x.Id),"updated"=>query.OrderByDescending(x=>x.UpdatedAt).ThenBy(x=>x.Id),_=>query.OrderBy(x=>x.Code).ThenBy(x=>x.Id)};
  var total=await query.CountAsync(); page=Math.Clamp(page,1,Math.Max(1,(int)Math.Ceiling(total/(double)size)));
  var result=new ListModel{Module=module,Total=total,Page=page,Size=size,Q=q,Status=status,Sort=sort,Rows=(await query.Skip((page-1)*size).Take(size).ToListAsync()).Cast<Record>().ToList(),Lookups=await Lookup.All(db)};
  if(typeof(T)==typeof(Invoice)) {
   var payments=await db.Payments.Where(x=>x.Status=="啟用").ToListAsync();
   foreach(var row in result.Rows.Cast<Invoice>()) result.Balances[row.Id]=row.Status=="作廢"?0:row.Amount-payments.Where(x=>x.InvoiceId==row.Id).Sum(x=>x.Amount);
  }
  if(typeof(T)==typeof(Unit)) {
   var today=Business.Today; var occupied=await db.Leases.Where(x=>x.Status=="有效"&&x.StartDate<=today&&x.EndDate>=today).Select(x=>x.UnitId).ToListAsync();
   foreach(var row in result.Rows) result.Occupancy[row.Id]=row.Status=="啟用"?(occupied.Contains(row.Id)?"出租中":"空置中"):row.Status;
  }
  return result;
 }
 public async Task<EditModel?> Edit(int id) {
  var record=id==0?new T():await db.Set<T>().AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);
  if(record==null)return null; if(id==0)record.Status=module.States[0];
  return await Model(record);
 }
 async Task<EditModel> Model(T record) {
  var result=new EditModel{Module=module,Id=record.Id,Version=record.Version};
  foreach(var f in module.Fields) {
   var value=typeof(T).GetProperty(f.Key)!.GetValue(record);
   result.Values[f.Key]=value is DateTime d?d.ToString(f.Kind=="month"?"yyyy-MM":"yyyy-MM-dd",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture)??"";
  }
  var lookups=await Lookup.All(db);
  foreach(var f in module.Fields.Where(x=>x.Reference!=null)) result.Options[f.Key]=lookups[f.Reference!].Select(x=>new Option(x.Key.ToString(),x.Value)).OrderBy(x=>x.Label).ToList();
  return result;
 }
 public async Task<EditModel> Save(int id,IFormCollection form) {
  await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  var record=id==0?new T{Id=RandomNumberGenerator.GetInt32(1,int.MaxValue)}:await db.Set<T>().FirstOrDefaultAsync(x=>x.Id==id);
  if(record==null) return new EditModel{Module=module,Id=id,Errors=["資料已不存在，請回到列表重新操作。"]};
  var result=await Model(record); result.Id=id;
  if(id!=0 && form["Version"]!=record.Version) {result.Errors.Add("其他人已修改此筆資料，請重新開啟後再編輯。");return result;}
  var original=new T(); db.Entry(original).CurrentValues.SetValues(record);
  var lookups=await Lookup.All(db);
  foreach(var f in module.Fields) {
   var raw=form[f.Key].ToString().Trim();result.Values[f.Key]=raw;
   if(f.Required&&raw.Length==0){result.Errors.Add($"請填寫{f.Label}。");continue;}
   if(raw.Length>2000){result.Errors.Add($"{f.Label}不可超過 2000 字。");continue;}
   var prop=typeof(T).GetProperty(f.Key)!;
   try {
    object value=prop.PropertyType==typeof(string)?raw:prop.PropertyType==typeof(int)?int.Parse(raw,CultureInfo.InvariantCulture):prop.PropertyType==typeof(decimal)?decimal.Parse(raw,CultureInfo.InvariantCulture):DateTime.ParseExact(raw,f.Kind=="month"?"yyyy-MM":"yyyy-MM-dd",CultureInfo.InvariantCulture);
    prop.SetValue(record,value);
    if(value is decimal number && (number<0||number>999999999m||decimal.Round(number,2)!=number)) result.Errors.Add($"{f.Label}須為 0 至 999,999,999，且最多兩位小數。");
    if(f.Choices!=null&&!f.Choices.Contains(raw)) result.Errors.Add($"{f.Label}選項無效。");
    if(f.Reference!=null&&(!int.TryParse(raw,out var fk)||!lookups[f.Reference].ContainsKey(fk)))result.Errors.Add($"請選擇有效的{f.Label}。");
    if(value is DateTime date&&(date.Year<1900||date.Year>2100))result.Errors.Add($"{f.Label}年份須介於 1900 至 2100 年。");
    if(f.Kind=="email"&&raw.Length>0&&!new EmailAddressAttribute().IsValid(raw))result.Errors.Add("電子信箱格式不正確。");
   }catch(Exception ex) when(ex is FormatException or OverflowException or ArgumentException){result.Errors.Add($"{f.Label}格式不正確。");}
  }
  var validation=new List<ValidationResult>();Validator.TryValidateObject(record,new ValidationContext(record),validation,true);
  result.Errors.AddRange(validation.Select(x=>x.ErrorMessage??"欄位無效。"));
  if(await db.Set<T>().AnyAsync(x=>x.Code==record.Code&&x.Id!=record.Id))result.Errors.Add("此編號已被使用，請改用其他編號。");
  if(result.Errors.Count==0) result.Errors.AddRange(await Business.Validate(db,record,id==0?null:original));
  if(result.Errors.Count>0){db.ChangeTracker.Clear();return result;}
  record.UpdatedAt=DateTime.UtcNow;record.Version=Guid.NewGuid().ToString();
  if(id==0) db.Set<T>().Add(record);
  try {await db.SaveChangesAsync();await transaction.CommitAsync();result.Errors.Clear();result.Id=record.Id;}
  catch(DbUpdateConcurrencyException){result.Errors.Add("資料已被修改，請重新載入。");}
  catch(DbUpdateException){result.Errors.Add("儲存失敗：資料重複或關聯已變更，請重新確認。");}
  return result;
 }
 public async Task<string?> Delete(int id,string version) {
  await using var transaction=await db.Database.BeginTransactionAsync();
  var record=await db.Set<T>().FirstOrDefaultAsync(x=>x.Id==id);if(record==null)return "資料已不存在。";
  if(record.Version!=version)return "資料已變更，請重新確認後再刪除。";
  // Financial history is retained; reversal is available through status changes.
  if(record is Payment or Invoice or Reading or EInvoice)return "帳務與抄表紀錄保留歷史，請使用編輯功能將狀態改為作廢或停用。";
  db.Remove(record);
  try {await db.SaveChangesAsync();await transaction.CommitAsync();return null;}catch(DbUpdateException){return "此資料已有關聯紀錄，無法刪除。請改為停用或終止，保留歷史資料。";}
 }
}
public static class Lookup {
 public static async Task<Dictionary<string,Dictionary<int,string>>> All(AppDbContext db)=>new(){
 ["assets"]=await db.Assets.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Code+" · "+x.Name),
 ["units"]=await db.Units.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Code+" · "+x.Name),
 ["tenants"]=await db.Tenants.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Code+" · "+x.Name),
 ["leases"]=await db.Leases.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Code+" · "+x.Name),
 ["meters"]=await db.Meters.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Code+" · "+x.Name),
 ["invoices"]=await db.Invoices.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Code+" · "+x.Name)
 };
}
