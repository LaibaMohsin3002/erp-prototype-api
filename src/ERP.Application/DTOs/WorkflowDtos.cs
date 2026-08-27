namespace ERP.Application.DTOs;

public record CreateWorkflowDefinitionDto(string Name, string DocumentType, decimal MinAmount, decimal MaxAmount, List<WorkflowStepInputDto> Steps);
public record CreateDelegationDto(string DelegateUserName, DateTime StartDate, DateTime EndDate, string? Reason,
    string? DocumentType, decimal? MinAmount, decimal? MaxAmount, string? OrgUnit);

public record DelegationDto(int DelegationId, string DelegatorUserName, string DelegateUserName,
    DateTime StartDate, DateTime EndDate, string? Reason, bool IsActive,
    string? DocumentType, decimal? MinAmount, decimal? MaxAmount, string? OrgUnit);
//public record WorkflowStepInputDto(int StepNo, string RequiredRole, int ParallelGroup, int TimeLimitHours, string? EscalationRole, string Priority);
//public record WorkflowStepDto(int WorkflowStepId, int StepNo, string RequiredRole, int ParallelGroup, int TimeLimitHours, string? EscalationRole, string Priority);
public record WorkflowStepInputDto(int StepNo, string RequiredRole, int ParallelGroup, int TimeLimitHours, string? EscalationRole, string Priority, bool ReassignOnTimeout);
public record WorkflowStepDto(int WorkflowStepId, int StepNo, string RequiredRole, int ParallelGroup, int TimeLimitHours, string? EscalationRole, string Priority, bool ReassignOnTimeout);
public record WorkflowActionDto(string Action, string? Comments); // Action: "Approve" | "Reject" | "Return" | "RequestInfo"
public record InboxTaskDto(
    int WorkflowTaskId, int WorkflowInstanceId, string DocumentType, int DocumentId,
    string DocumentNumber, string Requester, DateTime SubmittedDate, int StepNo,
    decimal? Amount, string Priority, DateTime? DueDate, string WorkflowStatus, DateTime CreatedDate);

public record WorkflowDefinitionListDto(int WorkflowDefinitionId, string Name, string DocumentType, decimal MinAmount, decimal MaxAmount, bool IsActive, int StepCount, int Version);

public record WorkflowDefinitionDetailDto(int WorkflowDefinitionId, string Name, string DocumentType, decimal MinAmount, decimal MaxAmount, bool IsActive, int Version, bool IsLatestVersion, int? PreviousVersionId, List<WorkflowStepDto> Steps);