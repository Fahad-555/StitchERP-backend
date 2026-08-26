using Microsoft.EntityFrameworkCore;
using StitchERP.Domain.Entities;

namespace StitchERP.Infrastructure.Data;

public class StitchErpDbContext : DbContext
{
    public StitchErpDbContext(DbContextOptions<StitchErpDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryLine> DeliveryLines => Set<DeliveryLine>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        MapOrganization(modelBuilder);
        MapProgram(modelBuilder);
        MapCustomer(modelBuilder);
        MapVendor(modelBuilder);
        MapP2P(modelBuilder);
        MapO2C(modelBuilder);
        MapInventory(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void MapOrganization(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Organization>();
        entity.ToTable("organizations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("organization_id").ValueGeneratedOnAdd();
        entity.Property(x => x.Name).HasColumnName("organization_name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at").IsRequired();
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at").IsRequired();
    }

    private static void MapProgram(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Program>();
        entity.ToTable("programs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("program_id").ValueGeneratedOnAdd();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.CustomerId).HasColumnName("customer_id");
        entity.Property(x => x.Code).HasColumnName("program_code").HasMaxLength(80).IsRequired();
        entity.Property(x => x.Name).HasColumnName("program_name").HasMaxLength(180).IsRequired();
        entity.Property(x => x.Status).HasColumnName("program_status").HasMaxLength(30).IsRequired();
        entity.Property(x => x.Brand).HasMaxLength(120);
        entity.Property(x => x.ThreadCount).HasColumnName("thread_count").HasMaxLength(50);
        entity.Property(x => x.WeaveDesign).HasColumnName("weave_design").HasMaxLength(120);
        entity.Property(x => x.Season).HasMaxLength(80);
        entity.Property(x => x.SaleOrderNumber).HasColumnName("sale_order_no").HasMaxLength(80);
        entity.Property(x => x.DeliveryDate).HasColumnName("delivery_date");
        entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(10);
        entity.Property(x => x.Remarks).HasColumnName("remarks");
        entity.Property(x => x.IsActive).HasColumnName("is_active");
        entity.Property(x => x.ProgramManagerId).HasColumnName("program_manager_id");
        entity.Property(x => x.LineManagerId).HasColumnName("line_manager_id");
        entity.Property(x => x.VersionNumber).HasColumnName("version_no");
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at").IsRequired();
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at").IsRequired();
    }

    private static void MapCustomer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Customer>();
        entity.ToTable("customers");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("customer_id").ValueGeneratedOnAdd();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.Code).HasColumnName("customer_code").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasColumnName("customer_name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
    }

    private static void MapVendor(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Vendor>();
        entity.ToTable("vendors");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("vendor_id").ValueGeneratedOnAdd();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.Code).HasColumnName("vendor_code").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasColumnName("vendor_name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
        entity.Property(x => x.PaymentTerms).HasColumnName("payment_terms").HasMaxLength(100);
    }

    private static void MapP2P(ModelBuilder modelBuilder)
    {
        MapDocument<PurchaseOrder>(modelBuilder, "purchase_orders", "po_id");
        MapColumns<PurchaseOrder>(modelBuilder, ("OrganizationId", "organization_id"), ("ProgramId", "program_id"), ("CustomerId", "customer_id"), ("Number", "po_number"), ("Status", "po_status"), ("OrderDate", "po_date"), ("DeliveryDate", "delivery_date"), ("CurrencyCode", "currency_code"), ("CreatedBy", "created_by"));
        MapDocument<PurchaseOrderLine>(modelBuilder, "purchase_order_lines", "po_line_id");
        MapColumns<PurchaseOrderLine>(modelBuilder, ("PurchaseOrderId", "po_id"), ("ItemReferenceId", "item_reference_id"), ("ItemType", "item_type"), ("Quantity", "quantity"), ("UnitRate", "unit_rate"), ("TotalAmount", "total_amount"), ("VendorId", "vendor_id"));
        MapDocument<GoodsReceipt>(modelBuilder, "goods_receipts", "goods_receipt_id");
        MapColumns<GoodsReceipt>(modelBuilder, ("OrganizationId", "organization_id"), ("PurchaseOrderId", "po_id"), ("VendorId", "vendor_id"), ("WarehouseId", "warehouse_id"), ("Number", "receipt_number"), ("Status", "receipt_status"), ("ReceiptDate", "receipt_date"), ("ReceivedBy", "received_by"));
        MapDocument<GoodsReceiptLine>(modelBuilder, "goods_receipt_lines", "goods_receipt_line_id");
        MapColumns<GoodsReceiptLine>(modelBuilder, ("GoodsReceiptId", "goods_receipt_id"), ("PurchaseOrderLineId", "po_line_id"), ("ReceivedQuantity", "received_qty"), ("AcceptedQuantity", "accepted_qty"), ("RejectedQuantity", "rejected_qty"), ("RejectionReason", "rejection_reason"));
        MapDocument<SupplierInvoice>(modelBuilder, "supplier_invoices", "supplier_invoice_id");
        MapColumns<SupplierInvoice>(modelBuilder, ("OrganizationId", "organization_id"), ("VendorId", "vendor_id"), ("PurchaseOrderId", "po_id"), ("Number", "invoice_number"), ("InvoiceDate", "invoice_date"), ("Status", "invoice_status"), ("TotalAmount", "total_amount"), ("MatchedAmount", "matched_amount"), ("CreatedBy", "created_by"));
        MapDocument<SupplierPayment>(modelBuilder, "supplier_payments", "supplier_payment_id");
        MapColumns<SupplierPayment>(modelBuilder, ("OrganizationId", "organization_id"), ("VendorId", "vendor_id"), ("SupplierInvoiceId", "supplier_invoice_id"), ("Number", "payment_number"), ("PaymentDate", "payment_date"), ("Method", "payment_method"), ("Status", "payment_status"), ("Amount", "amount"), ("CreatedBy", "created_by"));
    }

    private static void MapO2C(ModelBuilder modelBuilder)
    {
        MapDocument<SalesOrder>(modelBuilder, "sales_orders", "sales_order_id");
        MapColumns<SalesOrder>(modelBuilder, ("OrganizationId", "organization_id"), ("CustomerId", "customer_id"), ("ProgramId", "program_id"), ("Number", "order_number"), ("OrderDate", "order_date"), ("RequestedDeliveryDate", "requested_delivery_date"), ("Status", "order_status"), ("CurrencyCode", "currency_code"), ("TotalAmount", "total_amount"), ("CreatedBy", "created_by"));
        MapDocument<SalesOrderLine>(modelBuilder, "sales_order_lines", "sales_order_line_id");
        MapColumns<SalesOrderLine>(modelBuilder, ("SalesOrderId", "sales_order_id"), ("ProgramSkuId", "program_sku_id"), ("ItemReferenceId", "item_reference_id"), ("ItemType", "item_type"), ("OrderedQuantity", "ordered_qty"), ("ReservedQuantity", "reserved_qty"), ("DeliveredQuantity", "delivered_qty"), ("UnitPrice", "unit_price"), ("LineAmount", "line_amount"));
        MapDocument<StockReservation>(modelBuilder, "stock_reservations", "stock_reservation_id");
        MapColumns<StockReservation>(modelBuilder, ("OrganizationId", "organization_id"), ("SalesOrderLineId", "sales_order_line_id"), ("InventoryItemId", "inventory_item_id"), ("ReservedQuantity", "reserved_qty"), ("Status", "reservation_status"), ("CreatedBy", "created_by"));
        MapDocument<Delivery>(modelBuilder, "deliveries", "delivery_id");
        MapColumns<Delivery>(modelBuilder, ("OrganizationId", "organization_id"), ("CustomerId", "customer_id"), ("SalesOrderId", "sales_order_id"), ("WarehouseId", "warehouse_id"), ("Number", "delivery_number"), ("Status", "delivery_status"), ("DeliveryDate", "delivery_date"), ("ShippedBy", "shipped_by"));
        MapDocument<DeliveryLine>(modelBuilder, "delivery_lines", "delivery_line_id");
        MapColumns<DeliveryLine>(modelBuilder, ("DeliveryId", "delivery_id"), ("SalesOrderLineId", "sales_order_line_id"), ("Quantity", "quantity"));
        MapDocument<CustomerInvoice>(modelBuilder, "customer_invoices", "customer_invoice_id");
        MapColumns<CustomerInvoice>(modelBuilder, ("OrganizationId", "organization_id"), ("CustomerId", "customer_id"), ("SalesOrderId", "sales_order_id"), ("DeliveryId", "delivery_id"), ("Number", "invoice_number"), ("InvoiceDate", "invoice_date"), ("DueDate", "due_date"), ("Status", "invoice_status"), ("TotalAmount", "total_amount"), ("PaidAmount", "paid_amount"), ("CreatedBy", "created_by"));
        MapDocument<CustomerPayment>(modelBuilder, "customer_payments", "customer_payment_id");
        MapColumns<CustomerPayment>(modelBuilder, ("OrganizationId", "organization_id"), ("CustomerId", "customer_id"), ("CustomerInvoiceId", "customer_invoice_id"), ("Number", "payment_number"), ("PaymentDate", "payment_date"), ("Method", "payment_method"), ("Status", "payment_status"), ("Amount", "amount"), ("CreatedBy", "created_by"));
    }

    private static void MapInventory(ModelBuilder modelBuilder)
    {
        MapDocument<Warehouse>(modelBuilder, "warehouses", "warehouse_id");
        MapColumns<Warehouse>(modelBuilder, ("OrganizationId", "organization_id"), ("Code", "warehouse_code"), ("Name", "warehouse_name"), ("Status", "status"));
        MapDocument<InventoryItem>(modelBuilder, "inventory_items", "inventory_item_id");
        MapColumns<InventoryItem>(modelBuilder, ("WarehouseId", "warehouse_id"), ("ItemReferenceId", "item_reference_id"), ("ItemType", "item_type"), ("OnHandQuantity", "on_hand_qty"), ("ReservedQuantity", "reserved_qty"), ("AllocatedQuantity", "allocated_qty"), ("LastMovementAt", "last_movement_at"));
        MapDocument<InventoryTransaction>(modelBuilder, "inventory_transactions", "inventory_txn_id");
        MapColumns<InventoryTransaction>(modelBuilder, ("InventoryItemId", "inventory_item_id"), ("TransactionType", "transaction_type"), ("Quantity", "quantity"), ("ReferenceType", "reference_type"), ("ReferenceId", "reference_id"), ("CreatedBy", "created_by"), ("CreatedAt", "created_at"));
    }

    private static void MapDocument<TEntity>(ModelBuilder modelBuilder, string tableName, string keyColumn) where TEntity : class
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.ToTable(tableName);
        entity.HasKey("Id");
        entity.Property<long>("Id").HasColumnName(keyColumn).ValueGeneratedOnAdd();
    }

    private static void MapColumns<TEntity>(ModelBuilder modelBuilder, params (string Property, string Column)[] columns) where TEntity : class
    {
        var entity = modelBuilder.Entity<TEntity>();
        foreach (var (property, column) in columns)
        {
            entity.Property(property).HasColumnName(column);
        }
    }
}
