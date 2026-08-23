namespace ERP.Domain.Entities;

public class WorkflowDefinition
{
    public int WorkflowDefinitionId { get; set; }
    public string Name { get; set; } = default!;
    public string DocumentType { get; set; } = default!; // e.g. "PurchaseOrder"
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public bool IsActive { get; set; } = true;

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
}