using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Sales;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/sales/orders")]
public sealed class SalesOrdersController(IO2CService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("SALES_ORDER_VIEW")]
    public ActionResult<IReadOnlyCollection<SalesOrderSummary>> Get() => Ok(service.GetSalesOrders());

    [HttpPost]
    [RequirePermission("SALES_ORDER_CREATE")]
    public ActionResult<SalesOrderSummary> Create(CreateSalesOrderRequest request)
    {
        var order = service.CreateSalesOrder(request);
        return Created($"api/v1/sales/orders/{order.Id}", order);
    }

    [HttpPost("{id:long}/submit")]
    [RequirePermission("SALES_ORDER_EDIT")]
    public ActionResult<SalesOrderSummary> Submit(long id) => Ok(service.SubmitSalesOrder(id));

    [HttpPost("{id:long}/approve")]
    [RequirePermission("SALES_ORDER_APPROVE")]
    public ActionResult<SalesOrderSummary> Approve(long id) => Ok(service.ApproveSalesOrder(id));
}