namespace StitchERP.Domain.Entities;

public class Warehouse
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
}

public class InventoryItem
{
    public long Id { get; set; }
    public long WarehouseId { get; set; }
    public long ItemReferenceId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public DateTime? LastMovementAt { get; set; }
}

public class InventoryTransaction
{
    public long Id { get; set; }
    public long InventoryItemId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
