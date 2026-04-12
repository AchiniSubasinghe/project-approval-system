using Microsoft.AspNetCore.Identity;

namespace project_approval_system.Data;

public class ApplicationUser : IdentityUser
{
    public const int DefaultMaxProjectCapacity = 5;

    public string DisplayName { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public int MaxProjectCapacity { get; set; } = DefaultMaxProjectCapacity;

    public ICollection<SupervisorExpertise> Expertise { get; set; } = new List<SupervisorExpertise>();

    public ICollection<ProjectProposal> OwnedProposals { get; set; } = new List<ProjectProposal>();
}
