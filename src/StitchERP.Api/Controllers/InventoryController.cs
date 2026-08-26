using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Inventory;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/inventory")]
public sealed class InventoryController(IInventoryService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("INVENTORY_VIEW")]
    public ActionResult<IReadOnlyCollection<InventoryBalance>> GetBalances() => Ok(service.GetBalances());

    [HttpPost("stock")]
    [RequirePermission("INVENTORY_RECEIVE")]
    public ActionResult<InventoryTransactionResult> PostStock(PostStockRequest request)
        => Ok(service.PostStock(request));

    [HttpPost("reservations")]
    [RequirePermission("INVENTORY_RESERVE")]
    public ActionResult<InventoryTransactionResult> Reserve(ReserveStockRequest request)
        => Ok(service.Reserve(request));

    [HttpPost("reservations/release")]
    [RequirePermission("INVENTORY_RESERVE")]
    public ActionResult<InventoryTransactionResult> Release(ReserveStockRequest request)
        => Ok(service.Release(request));
}