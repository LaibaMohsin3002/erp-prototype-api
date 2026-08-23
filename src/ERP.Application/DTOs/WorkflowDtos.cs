namespace ERP.Application.DTOs;

//public record WorkflowStepInputDto(int StepNo, string RequiredRole, int ParallelGroup);

public record CreateWorkflowDefinitionDto(string Name, string DocumentType, decimal MinAmount, decimal MaxAmount, List<WorkflowStepInputDto> Steps);

//public record WorkflowStepDto(int WorkflowStepId, int StepNo, string RequiredRole, int ParallelGroup);

public record WorkflowDefinitionListDto(int WorkflowDefinitionId, string Name, string DocumentType, decimal MinAmount, decimal MaxAmount, bool IsActive, int StepCount);

public record WorkflowDefinitionDetailDto(int WorkflowDefinitionId, string Name, string DocumentType, decimal MinAmount, decimal MaxAmount, bool IsActive, List<WorkflowStepDto> Steps);
public record WorkflowActionDto(string Action, string? Comments); // Action: "Approve" | "Reject" | "Return"
public record InboxTaskDto(int WorkflowTaskId, int WorkflowInstanceId, string DocumentType, int DocumentId, string DocumentLabel, int StepNo, DateTime CreatedDate);
public record CreateDelegationDto(string DelegateUserName, DateTime StartDate, DateTime EndDate, string? Reason);
public record DelegationDto(int DelegationId, string DelegatorUserName, string DelegateUserName, DateTime StartDate, DateTime EndDate, string? Reason, bool IsActive);
public record WorkflowStepInputDto(int StepNo, string RequiredRole, int ParallelGroup, int TimeLimitHours, string? EscalationRole);

public record WorkflowStepDto(int WorkflowStepId, int StepNo, string RequiredRole, int ParallelGroup, int TimeLimitHours, string? EscalationRole);