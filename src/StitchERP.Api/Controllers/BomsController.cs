using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Programs;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/boms")]
public sealed class BomsController(IProgramBomService service) : ControllerBase
{
    [HttpPost]
    [RequirePermission("BOM_CREATE")]
    public ActionResult<BomSummary> CreateBom(CreateBomRequest request)
    {
        var bom = service.CreateBom(request);
        return Created($"api/v1/boms/{bom.Id}", bom);
    }

    [HttpPost("{id:long}/submit")]
    [RequirePermission("BOM_SUBMIT")]
    public ActionResult<BomSummary> SubmitBom(long id) => Ok(service.SubmitBom(id));
}