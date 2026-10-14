using System.Text.RegularExpressions;
using PropertyManagement.Models;

namespace PropertyManagement.Services;

public static class PropertySceneService
{
    public static List<SceneAsset> Build(IEnumerable<Asset> assets, IEnumerable<Unit> units,
        IEnumerable<Tenant> tenants, IEnumerable<Lease> leases, IEnumerable<Invoice> invoices,
        IEnumerable<Payment> payments, DateTime snapshot, DateTime today)
    {
        var tenantNames = tenants.ToDictionary(x => x.Id, x => x.Name);
        var allLeases = leases.ToList();
        var unitLeases = allLeases.ToLookup(x => x.UnitId);
        var leaseUnits = allLeases.ToDictionary(x => x.Id, x => x.UnitId);
        var paid = payments.Where(x => x.Status == "啟用").GroupBy(x => x.InvoiceId)
            .ToDictionary(x => x.Key, x => x.Sum(p => p.Amount));
        var overdue = invoices.Where(x => x.Status == "啟用" && x.DueDate < today && leaseUnits.ContainsKey(x.LeaseId))
            .GroupBy(x => leaseUnits[x.LeaseId])
            .ToDictionary(x => x.Key, x => x.Sum(i => Math.Max(0, i.Amount - paid.GetValueOrDefault(i.Id))));
        var assetUnits = units.ToLookup(x => x.AssetId);
        return assets.OrderBy(x => x.Code, StringComparer.Ordinal).Select(asset =>
        {
            var spaces = assetUnits[asset.Id].Select(unit =>
            {
                var lease = unitLeases[unit.Id].Where(x => x.Status == "有效" && x.StartDate <= snapshot && x.EndDate >= snapshot)
                    .OrderByDescending(x => x.StartDate).ThenBy(x => x.Id).FirstOrDefault();
                var status = asset.Status != "啟用" ? asset.Status : unit.Status != "啟用" ? unit.Status : lease == null ? "空置中" : "出租中";
                return new SceneUnit(unit.Id, unit.Code, unit.Name, FloorLabel(unit.Floor), status,
                    lease == null ? "" : tenantNames.GetValueOrDefault(lease.TenantId, ""), lease?.Id,
                    lease?.StartDate, lease?.EndDate, lease?.Rent ?? 0, overdue.GetValueOrDefault(unit.Id),
                    lease != null && lease.EndDate >= today && lease.EndDate <= today.AddDays(90));
            }).OrderBy(x => FloorOrder(x.Floor)).ThenBy(x => x.Floor, StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal).ToList();
            return new SceneAsset(asset.Id, asset.Code, asset.Name, asset.Category, asset.Address, asset.Status,
                spaces, spaces.Count(x => x.Status == "出租中"), spaces.Count(x => x.Status == "空置中"), spaces.Sum(x => x.Outstanding));
        }).ToList();
    }

    private static string FloorLabel(string value)
    {
        var label = value.Trim();
        if (label.Length == 0) return "未標示樓層";
        var match = Regex.Match(label, @"^(B\d+|\d+\s*(?:F|樓))(?:[-\s].*)?$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant().Replace(" ", "").Replace("樓", "F") : label;
    }

    private static int FloorOrder(string floor)
    {
        if (floor.StartsWith('B') && int.TryParse(floor.AsSpan(1), out var basement)) return -basement;
        return floor.EndsWith('F') && int.TryParse(floor.AsSpan(0, floor.Length - 1), out var level) ? level : int.MaxValue;
    }
}
