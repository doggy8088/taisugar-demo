using PropertyManagement.Models;
namespace PropertyManagement.Services;
public record Field(string Key,string Label,string Kind="text",bool Required=true,string[]? Choices=null,string? Reference=null);
public record Module(string Key,string Title,string Singular,string Description,string Icon,Type Type,Field[] Fields,string[] Columns,string[] States);
public static class Catalog {
 static Field F(string k,string l,string t="text",bool r=true)=>new(k,l,t,r);
 static Field S(string k,string l,params string[] choices)=>new(k,l,"select",true,choices);
 static Field R(string k,string l,string module)=>new(k,l,"reference",true,null,module);
 static Module M<T>(string key,string title,string singular,string desc,string icon,Field[] fields,string[] columns,string[]? states=null) where T:Record => new(key,title,singular,desc,icon,typeof(T),[F("Code","編號"),F("Name","名稱"),..fields,S("Status","狀態",states??["啟用","停用"]),F("Notes","備註","textarea",false)],columns,states??["啟用","停用"]);
 public static readonly Module[] All = [
 M<Asset>("assets","資產管理","資產","掌握每一處物業，建立完整的資產資料。","building",[S("Category","資產類型","商辦","店面","住宅","倉庫","停車場","其他"),F("Address","地址"),F("Company","所屬公司"),F("Manager","管理人"),F("Area","面積（坪）","number")],["Category","Address","Company"],["啟用","維修中","停用"]),
 M<Unit>("units","出租單位","出租單位","從整棟到每個空間，清楚管理可出租範圍。","grid",[R("AssetId","所屬資產","assets"),F("Floor","樓層／位置"),F("Area","面積（坪）","number")],["AssetId","Floor","Area"],["啟用","維修中","自用","停用"]),
 M<Tenant>("tenants","承租戶管理","承租戶","集中管理承租對象與聯絡資訊。","users",[S("Kind","承租戶類型","公司","個人"),F("TaxId","統一編號","text",false),F("Contact","聯絡人"),F("Phone","聯絡電話","tel"),F("Email","電子信箱","email",false),F("Address","聯絡地址","text",false)],["Kind","Contact","Phone"]),
 M<Lease>("leases","租約管理","租約","連結空間與承租戶，追蹤每份租約的生命週期。","file",[R("UnitId","出租單位","units"),R("TenantId","承租戶","tenants"),F("StartDate","起租日","date"),F("EndDate","到期日","date"),F("Rent","每月租金（NT$）","number"),F("Deposit","押金（NT$）","number"),F("DueDay","每月繳款日","integer")],["TenantId","EndDate","Rent"],["草稿","有效","已終止"]),
 M<Meter>("meters","水電資料","水電表","維護水電表、戶號與各單位的計費單價。","bolt",[R("UnitId","出租單位","units"),S("Kind","表計類型","電表","水表"),F("Account","水電戶號"),F("Rate","單價（NT$／度）","number")],["UnitId","Kind","Rate"]),
 M<Reading>("readings","抄表紀錄","抄表紀錄","保留每期讀數，依當期單價計算水電費用。","gauge",[R("MeterId","水電表","meters"),F("ReadDate","抄表日期","date"),F("Previous","前次讀數","number"),F("Current","本次讀數","number"),F("Rate","本期單價（NT$／度）","number")],["MeterId","ReadDate","Amount"]),
 M<Invoice>("invoices","應收帳款","應收帳款","每筆費用都有依據，未收餘額一目瞭然。","wallet",[R("LeaseId","租約","leases"),S("Kind","費用類型","租金","水費","電費","管理費","其他"),F("Period","帳款所屬月份","month"),F("DueDate","繳款期限","date"),F("Amount","應收金額（NT$）","number")],["Period","Amount","Balance"],["啟用","作廢"]),
 M<Payment>("payments","收款紀錄","收款紀錄","逐筆登錄實際入帳，自動更新應收餘額。","receipt",[R("InvoiceId","對應應收帳款","invoices"),F("PaidDate","收款日期","date"),F("Amount","收款金額（NT$）","number"),S("Method","收款方式","銀行轉帳","現金","支票","其他"),F("Reference","交易參考號","text",false)],["InvoiceId","PaidDate","Amount"],["啟用","作廢"])
 ];
 public static Module? Find(string key)=>All.FirstOrDefault(x=>x.Key==key);
}
public record Option(string Value,string Label);
public class ListModel {
 public Module Module {get;set;}=null!;
 public List<Record> Rows {get;set;}=[];
 public Dictionary<string,Dictionary<int,string>> Lookups {get;set;}=[];
 public Dictionary<int,decimal> Balances {get;set;}=[];
 public Dictionary<int,string> Occupancy {get;set;}=[];
 public int Total {get;set;} public int Page {get;set;}=1; public int Size {get;set;}=10;
 public string Q {get;set;}=""; public string Status {get;set;}=""; public string Sort {get;set;}="code";
 public int Pages=>Math.Max(1,(int)Math.Ceiling(Total/(double)Size));
 public string Cell(Record row,string key) {
  if(key=="Balance") return Balances.GetValueOrDefault(row.Id).ToString("N0");
  var f=Module.Fields.FirstOrDefault(x=>x.Key==key); var v=row.GetType().GetProperty(key)?.GetValue(row);
  if(f?.Reference is {} reference && v is int id) return Lookups.GetValueOrDefault(reference)?.GetValueOrDefault(id)??"—";
  if(v is DateTime date) return date.ToString(key=="Period"?"yyyy-MM":"yyyy/MM/dd");
  if(v is decimal number) return number.ToString(key is "Area" or "Rate"?"N2":"N0");
  return v?.ToString()??"—";
 }
}
public class EditModel {
 public Module Module {get;set;}=null!; public int Id {get;set;} public string Version {get;set;}="";
 public Dictionary<string,string> Values {get;set;}=[];
 public Dictionary<string,List<Option>> Options {get;set;}=[];
 public List<string> Errors {get;set;}=[];
 public string Value(string key)=>Values.GetValueOrDefault(key)??"";
}
