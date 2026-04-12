using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace project_approval_system.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ResearchArea> ResearchAreas => Set<ResearchArea>();
    public DbSet<SupervisorExpertise> SupervisorExpertise => Set<SupervisorExpertise>();
    public DbSet<ProjectProposal> Proposals => Set<ProjectProposal>();
    public DbSet<SupervisorInterest> SupervisorInterests => Set<SupervisorInterest>();
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ResearchArea>()
            .HasIndex(r => r.Name)
            .IsUnique();

        builder.Entity<ApplicationUser>()
            .Property(u => u.MaxProjectCapacity)
            .HasDefaultValue(ApplicationUser.DefaultMaxProjectCapacity);

        builder.Entity<SupervisorExpertise>()
            .HasKey(e => new { e.SupervisorId, e.ResearchAreaId });

        builder.Entity<SupervisorExpertise>()
            .HasOne(e => e.Supervisor)
            .WithMany(u => u.Expertise)
            .HasForeignKey(e => e.SupervisorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SupervisorExpertise>()
            .HasOne(e => e.ResearchArea)
            .WithMany(r => r.Supervisors)
            .HasForeignKey(e => e.ResearchAreaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectProposal>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.OwnedProposals)
            .HasForeignKey(p => p.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProjectProposal>()
            .HasOne(p => p.ResearchArea)
            .WithMany(r => r.Proposals)
            .HasForeignKey(p => p.ResearchAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SupervisorInterest>()
            .HasOne(i => i.Proposal)
            .WithMany(p => p.Interests)
            .HasForeignKey(i => i.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SupervisorInterest>()
            .HasOne(i => i.Supervisor)
            .WithMany()
            .HasForeignKey(i => i.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SupervisorInterest>()
            .HasIndex(i => new { i.ProposalId, i.SupervisorId })
            .IsUnique();

        builder.Entity<Match>()
            .HasOne(m => m.Proposal)
            .WithOne(p => p.Match!)
            .HasForeignKey<Match>(m => m.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Match>()
            .HasOne(m => m.Supervisor)
            .WithMany()
            .HasForeignKey(m => m.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Match>()
            .HasIndex(m => m.ProposalId)
            .IsUnique();
    }
}
