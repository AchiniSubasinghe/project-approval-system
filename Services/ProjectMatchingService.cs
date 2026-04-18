using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using project_approval_system.Data;

namespace project_approval_system.Services;

public sealed class ProjectMatchingService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> userManager) : IProjectMatchingService
{
    public async Task<OperationResult> ExpressInterestAsync(string supervisorId, int proposalId)
    {
        if (!await IsInRoleAsync(supervisorId, Roles.Supervisor))
        {
            return OperationResult.Failure("Only supervisors can express interest.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var proposal = await db.Proposals
            .Include(p => p.Match)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal is null)
        {
            return OperationResult.Failure("Proposal not found.");
        }

        if (proposal.Status == ProposalStatus.Withdrawn)
        {
            return OperationResult.Failure("Withdrawn proposals are not available.");
        }

        if (proposal.Status == ProposalStatus.Matched || proposal.Match is not null)
        {
            return OperationResult.Failure("This proposal is already matched.");
        }

        if (!await HasExpertiseAsync(db, supervisorId, proposal.ResearchAreaId))
        {
            return OperationResult.Failure("This proposal is outside your expertise areas.");
        }

        var duplicate = await db.SupervisorInterests
            .AnyAsync(i => i.ProposalId == proposalId && i.SupervisorId == supervisorId);
        if (duplicate)
        {
            return OperationResult.Failure("You have already expressed interest in this proposal.");
        }

        db.SupervisorInterests.Add(new SupervisorInterest
        {
            ProposalId = proposalId,
            SupervisorId = supervisorId,
            ExpressedAt = DateTime.UtcNow,
        });

        if (proposal.Status == ProposalStatus.Pending)
        {
            proposal.Status = ProposalStatus.UnderReview;
        }

        proposal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return OperationResult.Success("Interest recorded.");
    }

    public async Task<OperationResult> ConfirmMatchAsync(string supervisorId, int proposalId)
    {
        if (!await IsInRoleAsync(supervisorId, Roles.Supervisor))
        {
            return OperationResult.Failure("Only supervisors can confirm matches.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var proposal = await db.Proposals
            .Include(p => p.Match)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal is null)
        {
            return OperationResult.Failure("Proposal not found.");
        }

        if (proposal.Status == ProposalStatus.Withdrawn)
        {
            return OperationResult.Failure("Withdrawn proposals cannot be matched.");
        }

        if (proposal.Status == ProposalStatus.Matched || proposal.Match is not null)
        {
            return OperationResult.Failure("Another supervisor has already confirmed this proposal.");
        }

        if (!await HasExpertiseAsync(db, supervisorId, proposal.ResearchAreaId))
        {
            return OperationResult.Failure("This proposal is outside your expertise areas.");
        }

        var hasInterest = await db.SupervisorInterests
            .AnyAsync(i => i.ProposalId == proposalId && i.SupervisorId == supervisorId);
        if (!hasInterest)
        {
            return OperationResult.Failure("Express interest before confirming the match.");
        }

        var capacityResult = await HasCapacityAsync(db, supervisorId);
        if (!capacityResult.Succeeded)
        {
            return capacityResult;
        }

        CreateMatch(db, proposal, supervisorId);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return OperationResult.Success("Match confirmed. Identities are now revealed.");
    }

    public async Task<OperationResult> WithdrawProposalAsync(string studentId, int proposalId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return OperationResult.Failure("Sign-in required.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var proposal = await db.Proposals
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal is null)
        {
            return OperationResult.Failure("Proposal not found.");
        }

        if (proposal.OwnerUserId != studentId)
        {
            return OperationResult.Failure("You can only withdraw your own proposals.");
        }

        if (proposal.Status is not (ProposalStatus.Pending or ProposalStatus.UnderReview))
        {
            return OperationResult.Failure($"This proposal cannot be withdrawn (status: {proposal.Status}).");
        }

        proposal.Status = ProposalStatus.Withdrawn;
        proposal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return OperationResult.Success("Proposal withdrawn.");
    }

    public async Task<OperationResult> AdminAssignAsync(string moduleLeaderId, int proposalId, string supervisorId)
    {
        if (!await IsInRoleAsync(moduleLeaderId, Roles.ModuleLeader))
        {
            return OperationResult.Failure("Only module leaders can assign proposals.");
        }

        if (!await IsInRoleAsync(supervisorId, Roles.Supervisor))
        {
            return OperationResult.Failure("Select a supervisor account.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var proposal = await db.Proposals
            .Include(p => p.Match)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal is null)
        {
            return OperationResult.Failure("Proposal not found.");
        }

        if (proposal.Status == ProposalStatus.Withdrawn)
        {
            return OperationResult.Failure("Withdrawn proposals cannot be assigned.");
        }

        if (proposal.Status == ProposalStatus.Matched || proposal.Match is not null)
        {
            return OperationResult.Failure("Already matched proposals cannot be reassigned.");
        }

        var capacityResult = await HasCapacityAsync(db, supervisorId);
        if (!capacityResult.Succeeded)
        {
            return capacityResult;
        }

        CreateMatch(db, proposal, supervisorId);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return OperationResult.Success("Proposal assigned.");
    }

    private async Task<bool> IsInRoleAsync(string userId, string role)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId);
        return user is not null && await userManager.IsInRoleAsync(user, role);
    }

    private static async Task<OperationResult> HasCapacityAsync(ApplicationDbContext db, string supervisorId)
    {
        var supervisor = await db.Users.FirstOrDefaultAsync(u => u.Id == supervisorId);
        if (supervisor is null)
        {
            return OperationResult.Failure("Supervisor not found.");
        }

        var activeMatches = await db.Matches.CountAsync(m => m.SupervisorId == supervisorId);
        if (activeMatches >= supervisor.MaxProjectCapacity)
        {
            return OperationResult.Failure($"{supervisor.DisplayName} has reached the project capacity of {supervisor.MaxProjectCapacity}.");
        }

        return OperationResult.Success("Capacity available.");
    }

    private static Task<bool> HasExpertiseAsync(ApplicationDbContext db, string supervisorId, int researchAreaId) =>
        db.SupervisorExpertise.AnyAsync(e =>
            e.SupervisorId == supervisorId && e.ResearchAreaId == researchAreaId);

    private static void CreateMatch(ApplicationDbContext db, ProjectProposal proposal, string supervisorId)
    {
        db.Matches.Add(new Match
        {
            ProposalId = proposal.Id,
            SupervisorId = supervisorId,
            ConfirmedAt = DateTime.UtcNow,
        });

        proposal.Status = ProposalStatus.Matched;
        proposal.UpdatedAt = DateTime.UtcNow;
    }
}
