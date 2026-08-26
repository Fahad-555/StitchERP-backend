using Microsoft.AspNetCore.Mvc;
using StitchERP.Api.Security;
using StitchERP.Application.Governance;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/governance")]
public sealed class GovernanceController(IGovernanceService service) : ControllerBase
{
    [HttpGet("approvals")]
    public ActionResult<IReadOnlyCollection<ApprovalRequest>> Approvals() => Ok(service.GetApprovals());

    [HttpPost("approvals")]
    [RequirePermission("APPROVAL_SUBMIT")]
    public ActionResult<ApprovalRequest> RequestApproval(string entityType, long entityId, long organizationId)
    {
        var userId = GetUserId();
        return Ok(service.RequestApproval(entityType, entityId, userId, organizationId));
    }

    [HttpPost("approvals/{id:long}/approve")]
    [RequirePermission("APPROVAL_APPROVE")]
    public ActionResult<ApprovalRequest> Approve(long id) => Ok(service.Approve(id, GetUserId()));

    [HttpPost("approvals/{id:long}/reject")]
    [RequirePermission("APPROVAL_APPROVE")]
    public ActionResult<ApprovalRequest> Reject(long id, string reason) => Ok(service.Reject(id, GetUserId(), reason));

    [HttpGet("audit")]
    public ActionResult<IReadOnlyCollection<AuditEntry>> Audit() => Ok(service.GetAuditEntries());

    [HttpGet("notifications")]
    public ActionResult<IReadOnlyCollection<Notification>> Notifications() => Ok(service.GetNotifications(GetUserId()));

    private long GetUserId() => long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 1;
}