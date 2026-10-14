using System.ComponentModel.DataAnnotations;
namespace PropertyManagement.Models;
public abstract class Record {
 public int Id { get; set; }
 [Required, StringLength(40)] public string Code { get; set; } = "";
 [Required, StringLength(120)] public string Name { get; set; } = "";
 [Required, StringLength(30)] public string Status { get; set; } = "啟用";
 [StringLength(2000)] public string Notes { get; set; } = "";
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 [ConcurrencyCheck] public string Version { get; set; } = Guid.NewGuid().ToString();
}
public class Asset : Record {
 public string Category { get; set; } = "商辦";
 public string Address { get; set; } = "";
 public string Company { get; set; } = "總公司";
 public string Manager { get; set; } = "";
 public decimal Area { get; set; }
}
public class Unit : Record {
 public int AssetId { get; set; }
 public string Floor { get; set; } = "";
 public decimal Area { get; set; }
}
public class Tenant : Record {
 public string Kind { get; set; } = "公司";
 public string TaxId { get; set; } = "";
 public string Contact { get; set; } = "";
 public string Phone { get; set; } = "";
 public string Email { get; set; } = "";
 public string Address { get; set; } = "";
}
public class Lease : Record {
 public int UnitId { get; set; }
 public int TenantId { get; set; }
 public DateTime StartDate { get; set; } = DateTime.Today;
 public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1).AddDays(-1);
 public decimal Rent { get; set; }
 public decimal Deposit { get; set; }
 public int DueDay { get; set; } = 5;
}
public class Meter : Record {
 public int UnitId { get; set; }
 public string Kind { get; set; } = "電表";
 public string Account { get; set; } = "";
 public decimal Rate { get; set; } = 5;
}
public class Reading : Record {
 public int MeterId { get; set; }
 public DateTime ReadDate { get; set; } = DateTime.Today;
 public decimal Previous { get; set; }
 public decimal Current { get; set; }
 public decimal Rate { get; set; }
 public decimal Amount { get; set; }
}
public class Invoice : Record {
 public int LeaseId { get; set; }
 public string Kind { get; set; } = "租金";
 public DateTime Period { get; set; } = new(DateTime.Today.Year,DateTime.Today.Month,1);
 public DateTime DueDate { get; set; } = DateTime.Today;
 public decimal Amount { get; set; }
}
public class Payment : Record {
 public int InvoiceId { get; set; }
 public DateTime PaidDate { get; set; } = DateTime.Today;
 public decimal Amount { get; set; }
 public string Method { get; set; } = "銀行轉帳";
 public string Reference { get; set; } = "";
}
public class EInvoice : Record {
 public int InvoiceId { get; set; }
 public string TrackNumber { get; set; } = "";
 public DateTime IssuedDate { get; set; } = DateTime.Today;
 public string Category { get; set; } = "B2B 存證";
 public string TaxType { get; set; } = "應稅";
 public string BuyerTaxId { get; set; } = "";
 public string CarrierType { get; set; } = "無載具";
 public string CarrierId { get; set; } = "";
 public string DonateCode { get; set; } = "";
 public string RandomCode { get; set; } = "0000";
 public decimal SalesAmount { get; set; }
 public decimal TaxAmount { get; set; }
 public decimal Amount { get; set; }
}
