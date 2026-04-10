namespace project_approval_system.Data;

public class SupervisorInterest
{
    public int Id { get; set; }

    public int ProposalId { get; set; }
    public ProjectProposal Proposal { get; set; } = null!;

    public string SupervisorId { get; set; } = string.Empty;
    public ApplicationUser Supervisor { get; set; } = null!;

    public DateTime ExpressedAt { get; set; } = DateTime.UtcNow;
}
