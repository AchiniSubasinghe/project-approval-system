using System.ComponentModel.DataAnnotations;

namespace project_approval_system.Data;

public class ResearchArea
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SupervisorExpertise> Supervisors { get; set; } = new List<SupervisorExpertise>();

    public ICollection<ProjectProposal> Proposals { get; set; } = new List<ProjectProposal>();
}
