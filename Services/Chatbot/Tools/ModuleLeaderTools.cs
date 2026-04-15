using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using project_approval_system.Data;
using project_approval_system.Services.Chatbot.Models;

namespace project_approval_system.Services.Chatbot.Tools;

internal sealed class ModuleLeaderTools(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> userManager)
{
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new(
            "list_all_proposals",
            "List all proposals across the system with owner, research area, and match state. Optionally filter by status.",
            ToolSchemas.Parse("""{"type":"object","properties":{"status":{"type":"string","enum":["Pending","UnderReview","Matched","Withdrawn"],"description":"Optional status filter."}},"additionalProperties":false}""")),
        new(
            "list_unmatched_proposals",
            "List proposals that have no confirmed match yet (excludes Withdrawn).",
            ToolSchemas.Parse(ToolSchemas.Empty)),
        new(
            "list_supervisors_with_load",
            "List all supervisors with their max capacity and current confirmed-match count.",
            ToolSchemas.Parse(ToolSchemas.Empty)),
        new(
            "list_interests_for_proposal",
            "List all supervisors who have expressed interest in a specific proposal (module leaders may see identities).",
            ToolSchemas.Parse("""{"type":"object","properties":{"proposalId":{"type":"integer","description":"The proposal ID."}},"required":["proposalId"],"additionalProperties":false}""")),
        new(
            "get_matching_stats",
            "Get aggregate counts of proposals grouped by status.",
            ToolSchemas.Parse(ToolSchemas.Empty)),
    };

    public async Task<string> ExecuteAsync(string name, JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return name switch
        {
            "list_all_proposals" => await ListAllProposals(db, args, ct),
            "list_unmatched_proposals" => await ListUnmatchedProposals(db, ct),
            "list_supervisors_with_load" => await ListSupervisorsWithLoad(db, ct),
            "list_interests_for_proposal" => await ListInterestsForProposal(db, args, ct),
            "get_matching_stats" => await GetMatchingStats(db, ct),
            _ => throw new ArgumentException($"Unknown module leader tool '{name}'."),
        };
    }

    private static async Task<string> ListAllProposals(ApplicationDbContext db, JsonElement args, CancellationToken ct)
    {
        var statusFilter = ToolSchemas.GetOptionalStringProperty(args, "status");
        ProposalStatus? status = null;
        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<ProposalStatus>(statusFilter, ignoreCase: true, out var parsed))
        {
            status = parsed;
        }

        var query = db.Proposals.AsNoTracking().AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                researchArea = p.ResearchArea.Name,
                status = p.Status.ToString(),
                ownerName = p.Owner.DisplayName,
                ownerEmail = p.Owner.ContactEmail,
                matchedSupervisor = p.Match == null ? null : p.Match.Supervisor.DisplayName,
                createdAt = p.CreatedAt,
            })
            .ToListAsync(ct);

        return ToolSchemas.Serialize(new { proposals = rows });
    }

    private static async Task<string> ListUnmatchedProposals(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.Proposals
            .AsNoTracking()
            .Where(p => p.Match == null && p.Status != ProposalStatus.Withdrawn)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                researchArea = p.ResearchArea.Name,
                status = p.Status.ToString(),
                ownerName = p.Owner.DisplayName,
                interestCount = p.Interests.Count,
                createdAt = p.CreatedAt,
            })
            .ToListAsync(ct);

        return ToolSchemas.Serialize(new { unmatchedProposals = rows });
    }

    private async Task<string> ListSupervisorsWithLoad(ApplicationDbContext db, CancellationToken ct)
    {
        var supervisors = await userManager.GetUsersInRoleAsync(Roles.Supervisor);
        var ids = supervisors.Select(s => s.Id).ToList();

        var loads = await db.Matches
            .AsNoTracking()
            .Where(m => ids.Contains(m.SupervisorId))
            .GroupBy(m => m.SupervisorId)
            .Select(g => new { SupervisorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.SupervisorId, g => g.Count, ct);

        var rows = supervisors
            .Select(s => new
            {
                id = s.Id,
                displayName = s.DisplayName,
                email = s.ContactEmail,
                maxCapacity = s.MaxProjectCapacity,
                currentLoad = loads.GetValueOrDefault(s.Id, 0),
                available = Math.Max(0, s.MaxProjectCapacity - loads.GetValueOrDefault(s.Id, 0)),
            })
            .OrderBy(r => r.displayName)
            .ToList();

        return ToolSchemas.Serialize(new { supervisors = rows });
    }

    private static async Task<string> ListInterestsForProposal(ApplicationDbContext db, JsonElement args, CancellationToken ct)
    {
        var proposalId = ToolSchemas.GetIntProperty(args, "proposalId");
        var rows = await db.SupervisorInterests
            .AsNoTracking()
            .Where(i => i.ProposalId == proposalId)
            .OrderBy(i => i.ExpressedAt)
            .Select(i => new
            {
                supervisorId = i.SupervisorId,
                supervisorName = i.Supervisor.DisplayName,
                supervisorEmail = i.Supervisor.ContactEmail,
                expressedAt = i.ExpressedAt,
            })
            .ToListAsync(ct);

        return ToolSchemas.Serialize(new { proposalId, interests = rows });
    }

    private static async Task<string> GetMatchingStats(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.Proposals
            .AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { status = g.Key.ToString(), count = g.Count() })
            .ToListAsync(ct);

        var totalProposals = rows.Sum(r => r.count);
        var totalMatches = await db.Matches.AsNoTracking().CountAsync(ct);

        return ToolSchemas.Serialize(new
        {
            totalProposals,
            totalConfirmedMatches = totalMatches,
            byStatus = rows,
        });
    }
}
