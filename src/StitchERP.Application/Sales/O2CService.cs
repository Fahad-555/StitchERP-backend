using StitchERP.Application.Inventory;

namespace StitchERP.Application.Sales;

public sealed record SalesOrderLineRequest(long InventoryItemId, long? ProgramSkuId, string ItemType, string Description, decimal Quantity, decimal UnitPrice);
public sealed record CreateSalesOrderRequest(long OrganizationId, long CustomerId, long? ProgramId, string CurrencyCode, IReadOnlyCollection<SalesOrderLineRequest> Lines, long CreatedBy);
public sealed record SalesOrderLineSummary(long Id, long InventoryItemId, string Description, decimal OrderedQuantity, decimal ReservedQuantity, decimal DeliveredQuantity, decimal UnitPrice, decimal LineAmount);
public sealed record SalesOrderSummary(long Id, string Number, long CustomerId, long? ProgramId, string Status, decimal TotalAmount, IReadOnlyCollection<SalesOrderLineSummary> Lines);
public sealed record ReserveSalesOrderRequest(long SalesOrderId, long SalesOrderLineId, decimal Quantity, long CreatedBy);
public sealed record DeliverySummary(long Id, string Number, long SalesOrderId, string Status, decimal Quantity);
public sealed record CreateCustomerPaymentRequest(long CustomerId, long CustomerInvoiceId, decimal Amount, string Method, long CreatedBy);
public sealed record CustomerInvoiceSummary(long Id, string Number, long SalesOrderId, string Status, decimal TotalAmount, decimal PaidAmount);

public interface IO2CService
{
    IReadOnlyCollection<SalesOrderSummary> GetSalesOrders();
    SalesOrderSummary CreateSalesOrder(CreateSalesOrderRequest request);
    SalesOrderSummary SubmitSalesOrder(long id);
    SalesOrderSummary ApproveSalesOrder(long id);
    SalesOrderSummary ReserveStock(ReserveSalesOrderRequest request);
    DeliverySummary CreateDelivery(long salesOrderId, long salesOrderLineId, decimal quantity, long warehouseId, long shippedBy);
    CustomerInvoiceSummary CreateInvoice(long salesOrderId, long createdBy);
    CustomerInvoiceSummary PostPayment(CreateCustomerPaymentRequest request);
}

public sealed class O2CService(IInventoryService inventory) : IO2CService
{
    private readonly object sync = new();
    private readonly List<SalesOrderRecord> orders = [];
    private readonly List<CustomerInvoiceRecord> invoices = [];
    private long nextOrderId;
    private long nextLineId;
    private long nextDeliveryId;
    private long nextInvoiceId;

    public IReadOnlyCollection<SalesOrderSummary> GetSalesOrders()
    {
        lock (sync) return orders.Select(ToSummary).ToArray();
    }

    public SalesOrderSummary CreateSalesOrder(CreateSalesOrderRequest request)
    {
        if (request.OrganizationId <= 0 || request.CustomerId <= 0 || request.CreatedBy <= 0)
            throw new ArgumentException("OrganizationId, CustomerId and CreatedBy must be positive.");
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.Lines.Count == 0)
            throw new ArgumentException("CurrencyCode and at least one sales order line are required.");
        if (request.Lines.Any(x => x.InventoryItemId <= 0 || x.Quantity <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.Description)))
            throw new ArgumentException("Sales order lines contain invalid values.");

        lock (sync)
        {
            var record = new SalesOrderRecord(++nextOrderId, $"SO-DEV-{nextOrderId:000000}", request.OrganizationId, request.CustomerId, request.ProgramId, "DRAFT", request.Lines.Select(x => new SalesLine(++nextLineId, x.InventoryItemId, x.ProgramSkuId, x.ItemType, x.Description, x.Quantity, x.UnitPrice)).ToList());
            orders.Add(record);
            return ToSummary(record);
        }
    }

    public SalesOrderSummary SubmitSalesOrder(long id) => ChangeStatus(id, "DRAFT", "SUBMITTED");
    public SalesOrderSummary ApproveSalesOrder(long id) => ChangeStatus(id, "SUBMITTED", "APPROVED");

    public SalesOrderSummary ReserveStock(ReserveSalesOrderRequest request)
    {
        if (request.Quantity <= 0 || request.CreatedBy <= 0) throw new ArgumentException("Reservation quantity and CreatedBy must be positive.");
        lock (sync)
        {
            var order = FindOrder(request.SalesOrderId);
            if (order.Status != "APPROVED") throw new InvalidOperationException("Only approved sales orders can reserve stock.");
            var line = order.Lines.FirstOrDefault(x => x.Id == request.SalesOrderLineId) ?? throw new KeyNotFoundException("Sales order line was not found.");
            if (line.ReservedQuantity + request.Quantity > line.OrderedQuantity) throw new InvalidOperationException("Reserved quantity cannot exceed ordered quantity.");
            inventory.Reserve(new ReserveStockRequest(line.InventoryItemId, request.Quantity, request.CreatedBy));
            line.ReservedQuantity += request.Quantity;
            order.Status = "ALLOCATED";
            return ToSummary(order);
        }
    }

    public DeliverySummary CreateDelivery(long salesOrderId, long salesOrderLineId, decimal quantity, long warehouseId, long shippedBy)
    {
        if (quantity <= 0 || warehouseId <= 0 || shippedBy <= 0) throw new ArgumentException("Quantity, WarehouseId and ShippedBy must be positive.");
        lock (sync)
        {
            var order = FindOrder(salesOrderId);
            if (order.Status != "ALLOCATED") throw new InvalidOperationException("Stock must be reserved before delivery.");
            var line = order.Lines.FirstOrDefault(x => x.Id == salesOrderLineId) ?? throw new KeyNotFoundException("Sales order line was not found.");
            if (line.DeliveredQuantity + quantity > line.ReservedQuantity) throw new InvalidOperationException("Delivered quantity cannot exceed reserved quantity.");
            inventory.Release(new ReserveStockRequest(line.InventoryItemId, quantity, shippedBy));
            inventory.PostStock(new PostStockRequest(warehouseId, line.InventoryItemId, line.ItemType, quantity, "ISSUE", "DELIVERY", salesOrderId, shippedBy));
            line.DeliveredQuantity += quantity;
            order.Status = line.DeliveredQuantity == line.OrderedQuantity ? "DELIVERED" : "PARTIALLY_DELIVERED";
            return new DeliverySummary(++nextDeliveryId, $"DO-DEV-{nextDeliveryId:000000}", salesOrderId, "DELIVERED", quantity);
        }
    }

    public CustomerInvoiceSummary CreateInvoice(long salesOrderId, long createdBy)
    {
        if (createdBy <= 0) throw new ArgumentException("CreatedBy must be positive.");
        lock (sync)
        {
            var order = FindOrder(salesOrderId);
            var delivered = order.Lines.Sum(x => x.DeliveredQuantity);
            if (delivered <= 0) throw new InvalidOperationException("Invoice requires delivered quantity.");
            var total = order.Lines.Sum(x => x.DeliveredQuantity * x.UnitPrice);
            var invoice = new CustomerInvoiceRecord(++nextInvoiceId, $"CI-DEV-{nextInvoiceId:000000}", order.Id, order.CustomerId, total, 0, "APPROVED");
            invoices.Add(invoice);
            return ToSummary(invoice);
        }
    }

    public CustomerInvoiceSummary PostPayment(CreateCustomerPaymentRequest request)
    {
        if (request.CustomerId <= 0 || request.CustomerInvoiceId <= 0 || request.Amount <= 0 || request.CreatedBy <= 0 || string.IsNullOrWhiteSpace(request.Method))
            throw new ArgumentException("Valid customer, invoice, amount, method and CreatedBy are required.");
        lock (sync)
        {
            var invoice = invoices.FirstOrDefault(x => x.Id == request.CustomerInvoiceId) ?? throw new KeyNotFoundException("Customer invoice was not found.");
            if (invoice.CustomerId != request.CustomerId) throw new InvalidOperationException("Payment customer does not match the invoice customer.");
            if (request.Amount > invoice.TotalAmount - invoice.PaidAmount) throw new InvalidOperationException("Payment exceeds the remaining invoice balance.");
            invoice.PaidAmount += request.Amount;
            invoice.Status = invoice.PaidAmount == invoice.TotalAmount ? "PAID" : "PARTIALLY_PAID";
            return ToSummary(invoice);
        }
    }

    private SalesOrderSummary ChangeStatus(long id, string expected, string next)
    {
        lock (sync)
        {
            var order = FindOrder(id);
            if (order.Status != expected) throw new InvalidOperationException($"Only {expected} sales orders can move to {next}.");
            order.Status = next;
            return ToSummary(order);
        }
    }

    private SalesOrderRecord FindOrder(long id) => orders.FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Sales order was not found.");
    private static SalesOrderSummary ToSummary(SalesOrderRecord x) => new(x.Id, x.Number, x.CustomerId, x.ProgramId, x.Status, x.Lines.Sum(l => l.LineAmount), x.Lines.Select(l => new SalesOrderLineSummary(l.Id, l.InventoryItemId, l.Description, l.OrderedQuantity, l.ReservedQuantity, l.DeliveredQuantity, l.UnitPrice, l.LineAmount)).ToArray());
    private static CustomerInvoiceSummary ToSummary(CustomerInvoiceRecord x) => new(x.Id, x.Number, x.SalesOrderId, x.Status, x.TotalAmount, x.PaidAmount);

    private sealed class SalesOrderRecord(long id, string number, long organizationId, long customerId, long? programId, string status, List<SalesLine> lines)
    { public long Id { get; } = id; public string Number { get; } = number; public long OrganizationId { get; } = organizationId; public long CustomerId { get; } = customerId; public long? ProgramId { get; } = programId; public string Status { get; set; } = status; public List<SalesLine> Lines { get; } = lines; }
    private sealed class SalesLine(long id, long inventoryItemId, long? programSkuId, string itemType, string description, decimal orderedQuantity, decimal unitPrice)
    { public long Id { get; } = id; public long InventoryItemId { get; } = inventoryItemId; public long? ProgramSkuId { get; } = programSkuId; public string ItemType { get; } = itemType; public string Description { get; } = description; public decimal OrderedQuantity { get; } = orderedQuantity; public decimal UnitPrice { get; } = unitPrice; public decimal ReservedQuantity { get; set; } public decimal DeliveredQuantity { get; set; } public decimal LineAmount => OrderedQuantity * UnitPrice; }
    private sealed class CustomerInvoiceRecord(long id, string number, long salesOrderId, long customerId, decimal totalAmount, decimal paidAmount, string status)
    { public long Id { get; } = id; public string Number { get; } = number; public long SalesOrderId { get; } = salesOrderId; public long CustomerId { get; } = customerId; public decimal TotalAmount { get; } = totalAmount; public decimal PaidAmount { get; set; } = paidAmount; public string Status { get; set; } = status; }
}
