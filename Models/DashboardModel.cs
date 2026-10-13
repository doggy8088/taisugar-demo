namespace PropertyManagement.Models;
public record TrendPoint(string Month,decimal Expected,decimal Received);
public class DashboardModel {
 public DateTime Month {get;set;}
 public int Assets {get;set;} public int Units {get;set;} public int Occupied {get;set;}
 public decimal Expected {get;set;} public decimal Received {get;set;} public decimal Outstanding {get;set;}
 public decimal Occupancy=>Units==0?0:Math.Round(Occupied*100m/Units,1);
 public List<TrendPoint> Trends {get;set;}=[];
 public List<(Lease Lease,string Tenant,string Unit)> Expiring {get;set;}=[];
 public List<(Invoice Invoice,decimal Balance,string Tenant)> Unpaid {get;set;}=[];
 public List<(string Name,int Count)> Portfolio {get;set;}=[];
}
