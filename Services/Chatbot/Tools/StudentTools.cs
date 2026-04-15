using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using project_approval_system.Data;
using project_approval_system.Services.Chatbot.Models;

namespace project_approval_system.Services.Chatbot.Tools;

internal sealed class StudentTools(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new(
            "list_my_proposals",
            "List the signed-in student's project proposals with status, research area, and match state.",
            ToolSchemas.Parse(ToolSchemas.Empty)),
        new(
            "get_my_proposal_details",
            "Get full details of one of the student's own proposals, including research area and any confirmed match.",
            ToolSchemas.Parse("""{"type":"object","properties":{"proposalId":{"type":"integer","description":"The proposal ID."}},"required":["proposalId"],"additionalProperties":false}""")),
        new(
            "count_interest_on_my_proposal",
            "Count how many supervisors have expressed interest in a specific proposal owned by the student. Returns a count only — never supervisor names or identities.",
            ToolSchemas.Parse("""{"type":"object","properties":{"proposalId":{"type":"integer","description":"The proposal ID owned by the signed-in student."}},"required":["proposalId"],"additionalProperties":false}""")),
        new(
            "get_my_match",
            "Get the confirmed supervisor match for one of the student's proposals. Returns supervisor name and contact email only if the proposal status is 'Matched'.",
            ToolSchemas.Parse("""{"type":"object","properties":{"proposalId":{"type":"integer","description":"The proposal ID owned by the signed-in student."}},"required":["proposalId"],"additionalProperties":false}""")),
        new(
            "list_research_areas",
            "List all research areas available in the system (for reference when choosing a topic).",
            ToolSchemas.Parse(ToolSchemas.Empty)),
    };

    public async Task<string> ExecuteAsync(string name, JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return name switch
        {
            "list_my_proposals" => await ListMyProposals(db, ctx, ct),
            "get_my_proposal_details" => await GetMyProposalDetails(db, ctx, args, ct),
            "count_interest_on_my_proposal" => await CountInterestOnMyProposal(db, ctx, args, ct),
            "get_my_match" => await GetMyMatch(db, ctx, args, ct),
            "list_research_areas" => await ListResearchAreas(db, ct),
            _ => throw new ArgumentException($"Unknown student tool '{name}'."),
        };
    }

    private static async Task<string> ListMyProposals(ApplicationDbContext db, ChatToolContext ctx, CancellationToken ct)
    {
        var rows = await db.Proposals
            .AsNoTracking()
            .Where(p => p.OwnerUserId == ctx.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                researchArea = p.ResearchArea.Name,
                status = p.Status.ToString(),
                createdAt = p.CreatedAt,
                isMatched = p.Match != null,
            })
            .ToListAsync(ct);

        return ToolSchemas.Serialize(new { proposals = rows });
    }

    private static async Task<string> GetMyProposalDetails(ApplicationDbContext db, ChatToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var proposalId = ToolSchemas.GetIntProperty(args, "proposalId");
        var proposal = await db.Proposals
            .AsNoTracking()
            .Include(p => p.ResearchArea)
            .Include(p => p.Match!).ThenInclude(m => m.Supervisor)
            .Where(p => p.Id == proposalId && p.OwnerUserId == ctx.UserId)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                @abstract = p.Abstract,
                technicalStack = p.TechnicalStack,
                groupMembers = p.GroupMembers,
                researchArea = p.ResearchArea.Name,
                status = p.Status.ToString(),
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
                match = p.Match == null
                    ? null
                    : new
                    {
                        supervisorName = p.Match.Supervisor.DisplayName,
                        supervisorEmail = p.Match.Supervisor.ContactEmail,
                        confirmedAt = p.Match.ConfirmedAt,
                    },
            })
            .FirstOrDefaultAsync(ct);

        return proposal is null
            ? ToolSchemas.Serialize(new { error = "Proposal not found or not owned by you." })
            : ToolSchemas.Serialize(proposal);
    }

    private static async Task<string> CountInterestOnMyProposal(ApplicationDbContext db, ChatToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var proposalId = ToolSchemas.GetIntProperty(args, "proposalId");
        var owned = await db.Proposals
            .AsNoTracking()
            .AnyAsync(p => p.Id == proposalId && p.OwnerUserId == ctx.UserId, ct);

        if (!owned)
        {
            return ToolSchemas.Serialize(new { error = "Proposal not found or not owned by you." });
        }

        var count = await db.SupervisorInterests
            .AsNoTracking()
            .CountAsync(i => i.ProposalId == proposalId, ct);

        return ToolSchemas.Serialize(new { proposalId, interestedSupervisorCount = count });
    }

    private static async Task<string> GetMyMatch(ApplicationDbContext db, ChatToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var proposalId = ToolSchemas.GetIntProperty(args, "proposalId");
        var proposal = await db.Proposals
            .AsNoTracking()
            .Where(p => p.Id == proposalId && p.OwnerUserId == ctx.UserId)
            .Select(p => new
            {
                status = p.Status,
                match = p.Match == null
                    ? null
                    : new
                    {
                        supervisorName = p.Match.Supervisor.DisplayName,
                        supervisorEmail = p.Match.Supervisor.ContactEmail,
                        confirmedAt = p.Match.ConfirmedAt,
                    },
            })
            .FirstOrDefaultAsync(ct);

        if (proposal is null)
        {
            return ToolSchemas.Serialize(new { error = "Proposal not found or not owned by you." });
        }

        if (proposal.match is null || proposal.status != ProposalStatus.Matched)
        {
            return ToolSchemas.Serialize(new { proposalId, matched = false, status = proposal.status.ToString() });
        }

        return ToolSchemas.Serialize(new
        {
            proposalId,
            matched = true,
            status = proposal.status.ToString(),
            supervisorName = proposal.match.supervisorName,
            supervisorEmail = proposal.match.supervisorEmail,
            confirmedAt = proposal.match.confirmedAt,
        });
    }

    private static async Task<string> ListResearchAreas(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.ResearchAreas
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new { id = r.Id, name = r.Name })
            .ToListAsync(ct);
        return ToolSchemas.Serialize(new { researchAreas = rows });
    }
}
