namespace StitchERP.Domain.Entities;

public class Program
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public string? Brand { get; set; }
    public string? ThreadCount { get; set; }
    public string? WeaveDesign { get; set; }
    public string? Season { get; set; }
    public string? SaleOrderNumber { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Remarks { get; set; }
    public bool IsActive { get; set; } = true;
    public long? ProgramManagerId { get; set; }
    public long? LineManagerId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int VersionNumber { get; set; } = 1;
}
