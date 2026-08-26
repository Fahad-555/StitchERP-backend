namespace StitchERP.Domain.Entities;

public class Customer
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
}

public class Vendor
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public string? PaymentTerms { get; set; }
}

public class PurchaseOrder
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long? ProgramId { get; set; }
    public long CustomerId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public long CreatedBy { get; set; }
}

public class PurchaseOrderLine
{
    public long Id { get; set; }
    public long PurchaseOrderId { get; set; }
    public long ItemReferenceId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitRate { get; set; }
    public decimal TotalAmount { get; set; }
    public long? VendorId { get; set; }
}

public class GoodsReceipt
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long PurchaseOrderId { get; set; }
    public long VendorId { get; set; }
    public long WarehouseId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public DateTime ReceiptDate { get; set; }
    public long ReceivedBy { get; set; }
}

public class GoodsReceiptLine
{
    public long Id { get; set; }
    public long GoodsReceiptId { get; set; }
    public long PurchaseOrderLineId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? RejectionReason { get; set; }
}

public class SalesOrder
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long CustomerId { get; set; }
    public long? ProgramId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public long CreatedBy { get; set; }
}

public class SalesOrderLine
{
    public long Id { get; set; }
    public long SalesOrderId { get; set; }
    public long? ProgramSkuId { get; set; }
    public long? ItemReferenceId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
}

public class StockReservation
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long SalesOrderLineId { get; set; }
    public long InventoryItemId { get; set; }
    public decimal ReservedQuantity { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public long CreatedBy { get; set; }
}

public class Delivery
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long CustomerId { get; set; }
    public long SalesOrderId { get; set; }
    public long WarehouseId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public DateTime DeliveryDate { get; set; }
    public long ShippedBy { get; set; }
}

public class DeliveryLine
{
    public long Id { get; set; }
    public long DeliveryId { get; set; }
    public long SalesOrderLineId { get; set; }
    public decimal Quantity { get; set; }
}

public class CustomerInvoice
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long CustomerId { get; set; }
    public long? SalesOrderId { get; set; }
    public long? DeliveryId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public long CreatedBy { get; set; }
}

public class SupplierInvoice
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long VendorId { get; set; }
    public long? PurchaseOrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public decimal TotalAmount { get; set; }
    public decimal MatchedAmount { get; set; }
    public long CreatedBy { get; set; }
}

public class CustomerPayment
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long CustomerId { get; set; }
    public long? CustomerInvoiceId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public decimal Amount { get; set; }
    public long CreatedBy { get; set; }
}

public class SupplierPayment
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long VendorId { get; set; }
    public long? SupplierInvoiceId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public decimal Amount { get; set; }
    public long CreatedBy { get; set; }
}
