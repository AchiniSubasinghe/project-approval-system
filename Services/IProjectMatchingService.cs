namespace project_approval_system.Services;

public interface IProjectMatchingService
{
    Task<OperationResult> ExpressInterestAsync(string supervisorId, int proposalId);

    Task<OperationResult> ConfirmMatchAsync(string supervisorId, int proposalId);

    Task<OperationResult> AdminAssignAsync(string moduleLeaderId, int proposalId, string supervisorId);
}
