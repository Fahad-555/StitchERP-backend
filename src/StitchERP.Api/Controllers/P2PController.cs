using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Procurement;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/p2p")]
public sealed class P2PController(IP2PService service) : ControllerBase
{
    [HttpPost("goods-receipts")]
    [RequirePermission("PO_RECEIVE")]
    public ActionResult<GoodsReceiptSummary> Receive(CreateGoodsReceiptRequest request) => Ok(service.ReceiveGoods(request));

    [HttpPost("supplier-invoices/match")]
    [RequirePermission("INVOICE_CREATE")]
    public ActionResult<SupplierInvoiceSummary> MatchInvoice(CreateSupplierInvoiceRequest request) => Ok(service.CreateAndMatchInvoice(request));

    [HttpPost("supplier-payments/post")]
    [RequirePermission("PAYMENT_CREATE")]
    public ActionResult<SupplierPaymentSummary> PostPayment(CreateSupplierPaymentRequest request) => Ok(service.PostPayment(request));
}