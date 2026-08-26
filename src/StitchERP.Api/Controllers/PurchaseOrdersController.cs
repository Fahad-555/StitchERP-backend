using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Procurement;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
public sealed class PurchaseOrdersController(IP2PService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("PO_VIEW")]
    public ActionResult<IReadOnlyCollection<PurchaseOrderSummary>> Get() => Ok(service.GetPurchaseOrders());

    [HttpPost]
    [RequirePermission("PO_CREATE")]
    public ActionResult<PurchaseOrderSummary> Create(CreatePurchaseOrderRequest request)
    {
        var order = service.CreatePurchaseOrder(request);
        return Created($"api/v1/purchase-orders/{order.Id}", order);
    }

    [HttpPost("{id:long}/submit")]
    [RequirePermission("PO_SUBMIT")]
    public ActionResult<PurchaseOrderSummary> Submit(long id) => Ok(service.SubmitPurchaseOrder(id));

    [HttpPost("{id:long}/approve")]
    [RequirePermission("PO_APPROVE")]
    public ActionResult<PurchaseOrderSummary> Approve(long id) => Ok(service.ApprovePurchaseOrder(id));
}