namespace PropertyManagement.Models;

public record SceneUnit(int Id, string Code, string Name, string Floor, string Status,
    string Tenant, int? LeaseId, DateTime? StartDate, DateTime? EndDate,
    decimal Rent, decimal Outstanding, bool Expiring);

public record SceneAsset(int Id, string Code, string Name, string Category, string Address,
    string Status, List<SceneUnit> Units, int Occupied, int Available, decimal Outstanding);
