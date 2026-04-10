namespace project_approval_system.Data;

public class SupervisorExpertise
{
    public string SupervisorId { get; set; } = string.Empty;
    public ApplicationUser Supervisor { get; set; } = null!;

    public int ResearchAreaId { get; set; }
    public ResearchArea ResearchArea { get; set; } = null!;
}
