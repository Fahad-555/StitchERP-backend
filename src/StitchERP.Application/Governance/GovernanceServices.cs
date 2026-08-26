namespace StitchERP.Application.Governance;

public sealed record ApprovalRequest(long Id, string EntityType, long EntityId, string Status, long RequestedBy, long? ApproverId, DateTime CreatedAtUtc);
public sealed record AuditEntry(long Id, string EntityType, long EntityId, string Action, long PerformedBy, long OrganizationId, DateTime CreatedAtUtc);
public sealed record Notification(long Id, long UserId, string Type, string Title, string Message, bool IsRead, DateTime CreatedAtUtc);

public interface IGovernanceService
{
    ApprovalRequest RequestApproval(string entityType, long entityId, long requestedBy, long organizationId);
    ApprovalRequest Approve(long id, long approverId);
    ApprovalRequest Reject(long id, long approverId, string reason);
    IReadOnlyCollection<ApprovalRequest> GetApprovals();
    AuditEntry RecordAudit(string entityType, long entityId, string action, long performedBy, long organizationId);
    IReadOnlyCollection<AuditEntry> GetAuditEntries();
    Notification Notify(long userId, string type, string title, string message);
    IReadOnlyCollection<Notification> GetNotifications(long userId);
}

public sealed class GovernanceService : IGovernanceService
{
    private readonly object sync = new();
    private readonly List<ApprovalRequest> approvals = [];
    private readonly List<AuditEntry> auditEntries = [];
    private readonly List<Notification> notifications = [];
    private long nextApprovalId;
    private long nextAuditId;
    private long nextNotificationId;

    public ApprovalRequest RequestApproval(string entityType, long entityId, long requestedBy, long organizationId)
    {
        Validate(entityType, entityId, requestedBy, organizationId);
        lock (sync)
        {
            if (approvals.Any(x => x.EntityType == entityType && x.EntityId == entityId && x.Status == "PENDING"))
                throw new InvalidOperationException("An approval request is already pending for this document.");
            var request = new ApprovalRequest(++nextApprovalId, entityType, entityId, "PENDING", requestedBy, null, DateTime.UtcNow);
            approvals.Add(request);
            RecordAudit(entityType, entityId, "SUBMITTED_FOR_APPROVAL", requestedBy, organizationId);
            return request;
        }
    }

    public ApprovalRequest Approve(long id, long approverId)
    {
        lock (sync)
        {
            var index = approvals.FindIndex(x => x.Id == id);
            if (index < 0) throw new KeyNotFoundException("Approval request was not found.");
            var current = approvals[index];
            if (current.Status != "PENDING") throw new InvalidOperationException("Only pending approvals can be approved.");
            var updated = current with { Status = "APPROVED", ApproverId = approverId };
            approvals[index] = updated;
            return updated;
        }
    }

    public ApprovalRequest Reject(long id, long approverId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A rejection reason is required.");
        lock (sync)
        {
            var index = approvals.FindIndex(x => x.Id == id);
            if (index < 0) throw new KeyNotFoundException("Approval request was not found.");
            var current = approvals[index];
            if (current.Status != "PENDING") throw new InvalidOperationException("Only pending approvals can be rejected.");
            var updated = current with { Status = "REJECTED", ApproverId = approverId };
            approvals[index] = updated;
            return updated;
        }
    }

    public IReadOnlyCollection<ApprovalRequest> GetApprovals() { lock (sync) return approvals.ToArray(); }
    public AuditEntry RecordAudit(string entityType, long entityId, string action, long performedBy, long organizationId)
    {
        Validate(entityType, entityId, performedBy, organizationId);
        lock (sync) { var entry = new AuditEntry(++nextAuditId, entityType, entityId, action, performedBy, organizationId, DateTime.UtcNow); auditEntries.Add(entry); return entry; }
    }
    public IReadOnlyCollection<AuditEntry> GetAuditEntries() { lock (sync) return auditEntries.ToArray(); }
    public Notification Notify(long userId, string type, string title, string message)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Valid notification details are required.");
        lock (sync) { var notification = new Notification(++nextNotificationId, userId, type, title, message, false, DateTime.UtcNow); notifications.Add(notification); return notification; }
    }
    public IReadOnlyCollection<Notification> GetNotifications(long userId) { lock (sync) return notifications.Where(x => x.UserId == userId).ToArray(); }
    private static void Validate(string entityType, long entityId, long userId, long organizationId) { if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0 || userId <= 0 || organizationId <= 0) throw new ArgumentException("Valid entity, user and organization values are required."); }
}
