using System.ComponentModel.DataAnnotations;

namespace project_approval_system.Data;

public class ProjectProposal
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Abstract { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string TechnicalStack { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? GroupMembers { get; set; }

    public int ResearchAreaId { get; set; }
    public ResearchArea ResearchArea { get; set; } = null!;

    public string OwnerUserId { get; set; } = string.Empty;
    public ApplicationUser Owner { get; set; } = null!;

    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SupervisorInterest> Interests { get; set; } = new List<SupervisorInterest>();

    public Match? Match { get; set; }
}
