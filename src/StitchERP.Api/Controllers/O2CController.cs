using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Sales;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/o2c")]
public sealed class O2CController(IO2CService service) : ControllerBase
{
    [HttpPost("reservations")]
    [RequirePermission("INVENTORY_RESERVE")]
    public ActionResult<SalesOrderSummary> Reserve(ReserveSalesOrderRequest request) => Ok(service.ReserveStock(request));

    [HttpPost("deliveries")]
    [RequirePermission("DELIVERY_CREATE")]
    public ActionResult<DeliverySummary> Deliver(long salesOrderId, long salesOrderLineId, decimal quantity, long warehouseId, long shippedBy)
        => Ok(service.CreateDelivery(salesOrderId, salesOrderLineId, quantity, warehouseId, shippedBy));

    [HttpPost("invoices")]
    [RequirePermission("INVOICE_CREATE")]
    public ActionResult<CustomerInvoiceSummary> Invoice(long salesOrderId, long createdBy)
        => Ok(service.CreateInvoice(salesOrderId, createdBy));

    [HttpPost("payments")]
    [RequirePermission("PAYMENT_CREATE")]
    public ActionResult<CustomerInvoiceSummary> Payment(CreateCustomerPaymentRequest request)
        => Ok(service.PostPayment(request));
}