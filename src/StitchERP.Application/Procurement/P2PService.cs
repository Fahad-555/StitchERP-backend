using StitchERP.Application.Inventory;

namespace StitchERP.Application.Procurement;

public sealed record PurchaseOrderLineRequest(long ItemReferenceId, string ItemType, decimal Quantity, decimal UnitRate, long? VendorId);
public sealed record CreatePurchaseOrderRequest(long OrganizationId, long CustomerId, long? ProgramId, long VendorId, string CurrencyCode, IReadOnlyCollection<PurchaseOrderLineRequest> Lines);
public sealed record PurchaseOrderLineSummary(long Id, long ItemReferenceId, string ItemType, decimal Quantity, decimal UnitRate, decimal TotalAmount, long? VendorId, decimal ReceivedQuantity);
public sealed record PurchaseOrderSummary(long Id, string Number, long OrganizationId, long CustomerId, long VendorId, string Status, decimal TotalAmount, IReadOnlyCollection<PurchaseOrderLineSummary> Lines);
public sealed record CreateGoodsReceiptRequest(long PurchaseOrderId, long WarehouseId, IReadOnlyCollection<GoodsReceiptLineRequest> Lines, long ReceivedBy);
public sealed record GoodsReceiptLineRequest(long PurchaseOrderLineId, decimal ReceivedQuantity, decimal AcceptedQuantity, decimal RejectedQuantity);
public sealed record GoodsReceiptSummary(long Id, string Number, long PurchaseOrderId, string Status, decimal AcceptedQuantity);
public sealed record CreateSupplierInvoiceRequest(long VendorId, long PurchaseOrderId, decimal TotalAmount, decimal Quantity, long CreatedBy);
public sealed record SupplierInvoiceSummary(long Id, string Number, long PurchaseOrderId, string Status, decimal TotalAmount, decimal MatchedAmount);
public sealed record CreateSupplierPaymentRequest(long VendorId, long SupplierInvoiceId, decimal Amount, string Method, long CreatedBy);
public sealed record SupplierPaymentSummary(long Id, string Number, long SupplierInvoiceId, string Status, decimal Amount);

public interface IP2PService
{
    IReadOnlyCollection<PurchaseOrderSummary> GetPurchaseOrders();
    PurchaseOrderSummary CreatePurchaseOrder(CreatePurchaseOrderRequest request);
    PurchaseOrderSummary SubmitPurchaseOrder(long id);
    PurchaseOrderSummary ApprovePurchaseOrder(long id);
    GoodsReceiptSummary ReceiveGoods(CreateGoodsReceiptRequest request);
    SupplierInvoiceSummary CreateAndMatchInvoice(CreateSupplierInvoiceRequest request);
    SupplierPaymentSummary PostPayment(CreateSupplierPaymentRequest request);
}

public sealed class P2PService(IInventoryService inventory) : IP2PService
{
    private readonly object sync = new();
    private readonly List<PurchaseOrderRecord> orders = [];
    private readonly List<GoodsReceiptSummary> receipts = [];
    private readonly List<SupplierInvoiceRecord> invoices = [];
    private long nextOrderId;
    private long nextLineId;
    private long nextReceiptId;
    private long nextInvoiceId;
    private long nextPaymentId;

    public IReadOnlyCollection<PurchaseOrderSummary> GetPurchaseOrders()
    {
        lock (sync) return orders.Select(ToSummary).ToArray();
    }

    public PurchaseOrderSummary CreatePurchaseOrder(CreatePurchaseOrderRequest request)
    {
        if (request.OrganizationId <= 0 || request.CustomerId <= 0 || request.VendorId <= 0)
            throw new ArgumentException("OrganizationId, CustomerId and VendorId must be positive.");
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.Lines.Count == 0)
            throw new ArgumentException("CurrencyCode and at least one PO line are required.");
        if (request.Lines.Any(x => x.Quantity <= 0 || x.UnitRate < 0))
            throw new ArgumentException("PO quantities must be greater than zero and rates cannot be negative.");

        lock (sync)
        {
            var id = ++nextOrderId;
            var record = new PurchaseOrderRecord(id, $"PO-DEV-{id:000000}", request.OrganizationId, request.CustomerId, request.VendorId, "DRAFT", request.Lines.Select(x => new PurchaseLine(++nextLineId, x.ItemReferenceId, x.ItemType, x.Quantity, x.UnitRate, x.VendorId, 0)).ToList());
            orders.Add(record);
            return ToSummary(record);
        }
    }

    public PurchaseOrderSummary SubmitPurchaseOrder(long id) => ChangeOrderStatus(id, "DRAFT", "SUBMITTED");
    public PurchaseOrderSummary ApprovePurchaseOrder(long id) => ChangeOrderStatus(id, "SUBMITTED", "APPROVED");

    public GoodsReceiptSummary ReceiveGoods(CreateGoodsReceiptRequest request)
    {
        if (request.WarehouseId <= 0 || request.ReceivedBy <= 0 || request.Lines.Count == 0)
            throw new ArgumentException("WarehouseId, ReceivedBy and receipt lines are required.");
        lock (sync)
        {
            var order = FindOrder(request.PurchaseOrderId);
            if (order.Status != "APPROVED") throw new InvalidOperationException("Only approved purchase orders can receive goods.");
            decimal acceptedTotal = 0;
            foreach (var input in request.Lines)
            {
                var line = order.Lines.FirstOrDefault(x => x.Id == input.PurchaseOrderLineId) ?? throw new KeyNotFoundException("Purchase order line was not found.");
                if (input.ReceivedQuantity <= 0 || input.AcceptedQuantity < 0 || input.RejectedQuantity < 0 || input.AcceptedQuantity + input.RejectedQuantity > input.ReceivedQuantity)
                    throw new ArgumentException("Receipt quantities are invalid.");
                if (line.ReceivedQuantity + input.ReceivedQuantity > line.Quantity)
                    throw new InvalidOperationException("Received quantity cannot exceed the PO quantity.");
                line.ReceivedQuantity += input.ReceivedQuantity;
                acceptedTotal += input.AcceptedQuantity;
                if (input.AcceptedQuantity > 0)
                    inventory.PostStock(new PostStockRequest(request.WarehouseId, line.ItemReferenceId, line.ItemType, input.AcceptedQuantity, "RECEIPT", "GOODS_RECEIPT", request.PurchaseOrderId, request.ReceivedBy));
            }
            var receipt = new GoodsReceiptSummary(++nextReceiptId, $"GRN-DEV-{nextReceiptId:000000}", request.PurchaseOrderId, "POSTED", acceptedTotal);
            receipts.Add(receipt);
            return receipt;
        }
    }

    public SupplierInvoiceSummary CreateAndMatchInvoice(CreateSupplierInvoiceRequest request)
    {
        if (request.VendorId <= 0 || request.PurchaseOrderId <= 0 || request.TotalAmount < 0 || request.Quantity <= 0)
            throw new ArgumentException("Valid vendor, PO, amount and quantity are required.");
        lock (sync)
        {
            var order = FindOrder(request.PurchaseOrderId);
            if (order.VendorId != request.VendorId) throw new InvalidOperationException("Invoice vendor does not match the purchase order vendor.");
            var received = order.Lines.Sum(x => x.ReceivedQuantity);
            if (request.Quantity > received) throw new InvalidOperationException("Invoice quantity cannot exceed received quantity.");
            var invoice = new SupplierInvoiceRecord(++nextInvoiceId, $"SI-DEV-{nextInvoiceId:000000}", request.PurchaseOrderId, request.VendorId, request.TotalAmount, request.Quantity, "MATCHED");
            invoices.Add(invoice);
            return ToSummary(invoice);
        }
    }

    public SupplierPaymentSummary PostPayment(CreateSupplierPaymentRequest request)
    {
        if (request.VendorId <= 0 || request.SupplierInvoiceId <= 0 || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Method))
            throw new ArgumentException("Valid vendor, invoice, amount and payment method are required.");
        lock (sync)
        {
            var invoice = invoices.FirstOrDefault(x => x.Id == request.SupplierInvoiceId) ?? throw new KeyNotFoundException("Supplier invoice was not found.");
            if (invoice.VendorId != request.VendorId) throw new InvalidOperationException("Payment vendor does not match the invoice vendor.");
            if (invoice.Status != "MATCHED" && invoice.Status != "APPROVED") throw new InvalidOperationException("Only matched or approved invoices can be paid.");
            if (request.Amount > invoice.TotalAmount) throw new InvalidOperationException("Payment cannot exceed invoice amount.");
            invoice.Status = request.Amount == invoice.TotalAmount ? "PAID" : "PARTIALLY_PAID";
            return new SupplierPaymentSummary(++nextPaymentId, $"SP-DEV-{nextPaymentId:000000}", invoice.Id, invoice.Status, request.Amount);
        }
    }

    private PurchaseOrderSummary ChangeOrderStatus(long id, string expected, string next)
    {
        lock (sync)
        {
            var order = FindOrder(id);
            if (order.Status != expected) throw new InvalidOperationException($"Only {expected} purchase orders can move to {next}.");
            order.Status = next;
            return ToSummary(order);
        }
    }

    private PurchaseOrderRecord FindOrder(long id) => orders.FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Purchase order was not found.");
    private static PurchaseOrderSummary ToSummary(PurchaseOrderRecord x) => new(x.Id, x.Number, x.OrganizationId, x.CustomerId, x.VendorId, x.Status, x.Lines.Sum(l => l.TotalAmount), x.Lines.Select(l => new PurchaseOrderLineSummary(l.Id, l.ItemReferenceId, l.ItemType, l.Quantity, l.UnitRate, l.TotalAmount, l.VendorId, l.ReceivedQuantity)).ToArray());
    private static SupplierInvoiceSummary ToSummary(SupplierInvoiceRecord x) => new(x.Id, x.Number, x.PurchaseOrderId, x.Status, x.TotalAmount, x.TotalAmount);

    private sealed class PurchaseOrderRecord(long id, string number, long organizationId, long customerId, long vendorId, string status, List<PurchaseLine> lines)
    {
        public long Id { get; } = id; public string Number { get; } = number; public long OrganizationId { get; } = organizationId; public long CustomerId { get; } = customerId; public long VendorId { get; } = vendorId; public string Status { get; set; } = status; public List<PurchaseLine> Lines { get; } = lines;
    }
    private sealed class PurchaseLine(long id, long itemReferenceId, string itemType, decimal quantity, decimal unitRate, long? vendorId, decimal receivedQuantity)
    {
        public long Id { get; } = id; public long ItemReferenceId { get; } = itemReferenceId; public string ItemType { get; } = itemType; public decimal Quantity { get; } = quantity; public decimal UnitRate { get; } = unitRate; public long? VendorId { get; } = vendorId; public decimal ReceivedQuantity { get; set; } = receivedQuantity; public decimal TotalAmount => Quantity * UnitRate;
    }
    private sealed class SupplierInvoiceRecord(long id, string number, long purchaseOrderId, long vendorId, decimal totalAmount, decimal quantity, string status)
    {
        public long Id { get; } = id; public string Number { get; } = number; public long PurchaseOrderId { get; } = purchaseOrderId; public long VendorId { get; } = vendorId; public decimal TotalAmount { get; } = totalAmount; public decimal Quantity { get; } = quantity; public string Status { get; set; } = status;
    }
}
