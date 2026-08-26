using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Programs;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/programs")]
public sealed class ProgramsController(IProgramBomService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("PROGRAM_VIEW")]
    public ActionResult<object> GetPrograms() => Ok(new
    {
        items = service.GetPrograms(),
        page = 1,
        pageSize = 25,
        totalCount = service.GetPrograms().Count,
        totalPages = 1
    });

    [HttpPost]
    [RequirePermission("PROGRAM_CREATE")]
    public ActionResult<ProgramSummary> CreateProgram(CreateProgramRequest request)
    {
        var program = service.CreateProgram(request);
        return Created($"api/v1/programs/{program.Id}", program);
    }

    [HttpPost("{id:long}/submit")]
    [RequirePermission("PROGRAM_EDIT")]
    public ActionResult<ProgramSummary> SubmitProgram(long id) => Ok(service.SubmitProgram(id));
}