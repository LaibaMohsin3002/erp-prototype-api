namespace ERP.Domain.Entities;

public class WorkflowDefinition
{
    public int WorkflowDefinitionId { get; set; }
    public string Name { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public int? PreviousVersionId { get; set; }
    public bool IsLatestVersion { get; set; } = true;

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}

public class WorkflowStep
{
    public int WorkflowStepId { get; set; }
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition Definition { get; set; } = default!;
    public int StepNo { get; set; }
    public string RequiredRole { get; set; } = default!;
    public int ParallelGroup { get; set; } // steps sharing a group number run in parallel; 0 = not parallel
    public int TimeLimitHours { get; set; } = 0; // 0 = no limit
    public string? EscalationRole { get; set; } // who it escalates to if overdue; null = no escalation
    public string Priority { get; set; } = "Normal";
    public bool ReassignOnTimeout { get; set; } = false;
}

public class WorkflowInstance
{
    public int WorkflowInstanceId { get; set; }
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition Definition { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public int DocumentId { get; set; }
    public string Status { get; set; } = "InProgress"; // InProgress, Approved, Rejected
    public int CurrentStepNo { get; set; } = 1;
    public string StartedBy { get; set; } = default!;
    public DateTime StartedDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }

    public ICollection<WorkflowTask> Tasks { get; set; } = new List<WorkflowTask>();
}

public class WorkflowTask
{
    public int WorkflowTaskId { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance Instance { get; set; } = default!;
    public int StepNo { get; set; }
    public string AssignedRole { get; set; } = default!;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Returned
    public string? ActionedBy { get; set; }
    public DateTime? ActionedDate { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public bool IsEscalated { get; set; } = false;
    public bool ReminderSent { get; set; } = false;
}

public class Delegation
{
    public int DelegationId { get; set; }
    public int DelegatorUserId { get; set; }
    public User Delegator { get; set; } = default!;
    public int DelegateUserId { get; set; }
    public User Delegate { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
    public string? DocumentType { get; set; }   // e.g. "PurchaseOrder"; null = all document types
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? OrgUnit { get; set; }
}

public class WorkflowActionToken
{
    public int WorkflowActionTokenId { get; set; }
    public string Token { get; set; } = default!;
    public int WorkflowTaskId { get; set; }
    public WorkflowTask Task { get; set; } = default!;
    public string Action { get; set; } = default!; // "Approve" or "Reject"
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string RecipientEmail { get; set; } = default!;
}

public class WorkflowAuditEntry
{
    public int WorkflowAuditEntryId { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance Instance { get; set; } = default!;
    public int WorkflowVersion { get; set; }
    public string DocumentType { get; set; } = default!;
    public int DocumentId { get; set; }
    public int? StepNo { get; set; }
    public string? AssignedApproverRole { get; set; }
    public string ActingUser { get; set; } = default!;
    public string? DelegatedFromUser { get; set; }
    public string Action { get; set; } = default!;
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    public string? Comments { get; set; }
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? NotificationReference { get; set; } // "In-App", "Email", or a token id
}