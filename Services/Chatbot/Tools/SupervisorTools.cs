using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using project_approval_system.Data;
using project_approval_system.Services;
using project_approval_system.Services.Chatbot.Models;

namespace project_approval_system.Services.Chatbot.Tools;

internal sealed class SupervisorTools(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> userManager,
    IProjectMatchingService matching)
{
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new(
            "list_open_proposals_in_my_expertise",
            "List open (Pending or UnderReview) proposals in the supervisor's expertise areas. Anonymous — never returns the student owner's name or email.",
            ToolSchemas.Parse("""{"type":"object","properties":{"researchAreaId":{"type":"integer","description":"Optional: filter to a single research area ID from the supervisor's expertise."},"limit":{"type":"integer","description":"Max rows to return (default 20, max 50)."}},"additionalProperties":false}""")),
        new(
            "get_proposal_anon",
            "Get the anonymous details of a proposal (title, abstract, tech stack, group size, research area). Never returns the owner's identity. Use this for the blind-review flow.",
            ToolSchemas.Parse("""{"type":"object","properties":{"proposalId":{"type":"integer","description":"The proposal ID."}},"required":["proposalId"],"additionalProperties":false}""")),
        new(
            "list_my_interests",
            "List proposals this supervisor has expressed interest in. Anonymous — proposals are identified by title/ID only, no student identities.",
            ToolSchemas.Parse(ToolSchemas.Empty)),
        new(
            "list_my_confirmed_matches",
            "List confirmed matches for this supervisor. Because identity reveal has occurred, includes the student's display name and contact email.",
            ToolSchemas.Parse(ToolSchemas.Empty)),
        new(
            "get_my_capacity",
            "Get the supervisor's project capacity and current load.",
            ToolSchemas.Parse(ToolSchemas.Empty)),
        new(
            "express_interest",
            "Express interest in an open proposal (anonymous — the student is not notified of your identity until you confirm a match). Requires the proposal to be in your expertise area and not already matched. This is a write action — the server will pause to ask the user to confirm before it actually runs.",
            ToolSchemas.Parse("""{"type":"object","properties":{"proposalId":{"type":"integer","description":"The proposal ID."}},"required":["proposalId"],"additionalProperties":false}""")),
        new(
            "confirm_match",
            "Confirm a match on a proposal you have already expressed interest in. This reveals the student's identity. Requires available capacity. This is a write action — the server will pause to ask the user to confirm before it actually runs.",
            ToolSchemas.Parse("""{"type":"object","properties":{"proposalId":{"type":"integer","description":"The proposal ID you are confirming a match on."}},"required":["proposalId"],"additionalProperties":false}""")),
    };

    public async Task<string> ExecuteAsync(string name, JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        if (name == "express_interest")
        {
            return await ExpressInterest(ctx, args);
        }
        if (name == "confirm_match")
        {
            return await ConfirmMatch(ctx, args);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return name switch
        {
            "list_open_proposals_in_my_expertise" => await ListOpenProposals(db, ctx, args, ct),
            "get_proposal_anon" => await GetProposalAnon(db, args, ct),
            "list_my_interests" => await ListMyInterests(db, ctx, ct),
            "list_my_confirmed_matches" => await ListMyConfirmedMatches(db, ctx, ct),
            "get_my_capacity" => await GetMyCapacity(db, ctx, ct),
            _ => throw new ArgumentException($"Unknown supervisor tool '{name}'."),
        };
    }

    private async Task<string> ExpressInterest(ChatToolContext ctx, JsonElement args)
    {
        var proposalId = ToolSchemas.GetIntProperty(args, "proposalId");
        var result = await matching.ExpressInterestAsync(ctx.UserId, proposalId);
        return ToolSchemas.Serialize(new
        {
            succeeded = result.Succeeded,
            message = result.Message,
            proposalId,
        });
    }

    private async Task<string> ConfirmMatch(ChatToolContext ctx, JsonElement args)
    {
        var proposalId = ToolSchemas.GetIntProperty(args, "proposalId");
        var result = await matching.ConfirmMatchAsync(ctx.UserId, proposalId);
        return ToolSchemas.Serialize(new
        {
            succeeded = result.Succeeded,
            message = result.Message,
            proposalId,
        });
    }

    private static async Task<string> ListOpenProposals(ApplicationDbContext db, ChatToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var limit = Math.Clamp(ToolSchemas.GetOptionalIntProperty(args, "limit") ?? 20, 1, 50);
        var researchAreaId = ToolSchemas.GetOptionalIntProperty(args, "researchAreaId");

        var expertiseAreaIds = await db.SupervisorExpertise
            .AsNoTracking()
            .Where(e => e.SupervisorId == ctx.UserId)
            .Select(e => e.ResearchAreaId)
            .ToListAsync(ct);

        if (expertiseAreaIds.Count == 0)
        {
            return ToolSchemas.Serialize(new
            {
                proposals = Array.Empty<object>(),
                note = "No expertise areas set. Configure expertise on your dashboard first.",
            });
        }

        if (researchAreaId.HasValue && !expertiseAreaIds.Contains(researchAreaId.Value))
        {
            return ToolSchemas.Serialize(new
            {
                error = "Requested research area is not in your expertise.",
            });
        }

        var query = db.Proposals
            .AsNoTracking()
            .Where(p => (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.UnderReview)
                        && p.Match == null
                        && expertiseAreaIds.Contains(p.ResearchAreaId));

        if (researchAreaId.HasValue)
        {
            query = query.Where(p => p.ResearchAreaId == researchAreaId.Value);
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                researchArea = p.ResearchArea.Name,
                status = p.Status.ToString(),
                createdAt = p.CreatedAt,
                alreadyExpressedInterest = p.Interests.Any(i => i.SupervisorId == ctx.UserId),
            })
            .ToListAsync(ct);

        return ToolSchemas.Serialize(new { proposals = rows });
    }

    private static async Task<string> GetProposalAnon(ApplicationDbContext db, JsonElement args, CancellationToken ct)
    {
        var proposalId = ToolSchemas.GetIntProperty(args, "proposalId");
        var proposal = await db.Proposals
            .AsNoTracking()
            .Where(p => p.Id == proposalId)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                @abstract = p.Abstract,
                technicalStack = p.TechnicalStack,
                isGroupProposal = !string.IsNullOrWhiteSpace(p.GroupMembers),
                researchArea = p.ResearchArea.Name,
                status = p.Status.ToString(),
                isMatched = p.Match != null,
            })
            .FirstOrDefaultAsync(ct);

        return proposal is null
            ? ToolSchemas.Serialize(new { error = "Proposal not found." })
            : ToolSchemas.Serialize(proposal);
    }

    private static async Task<string> ListMyInterests(ApplicationDbContext db, ChatToolContext ctx, CancellationToken ct)
    {
        var rows = await db.SupervisorInterests
            .AsNoTracking()
            .Where(i => i.SupervisorId == ctx.UserId)
            .OrderByDescending(i => i.ExpressedAt)
            .Select(i => new
            {
                proposalId = i.ProposalId,
                title = i.Proposal.Title,
                researchArea = i.Proposal.ResearchArea.Name,
                proposalStatus = i.Proposal.Status.ToString(),
                isMatchedToMe = i.Proposal.Match != null && i.Proposal.Match.SupervisorId == ctx.UserId,
                expressedAt = i.ExpressedAt,
            })
            .ToListAsync(ct);

        return ToolSchemas.Serialize(new { interests = rows });
    }

    private static async Task<string> ListMyConfirmedMatches(ApplicationDbContext db, ChatToolContext ctx, CancellationToken ct)
    {
        var rows = await db.Matches
            .AsNoTracking()
            .Where(m => m.SupervisorId == ctx.UserId)
            .OrderByDescending(m => m.ConfirmedAt)
            .Select(m => new
            {
                proposalId = m.ProposalId,
                title = m.Proposal.Title,
                researchArea = m.Proposal.ResearchArea.Name,
                studentName = m.Proposal.Owner.DisplayName,
                studentEmail = m.Proposal.Owner.ContactEmail,
                confirmedAt = m.ConfirmedAt,
            })
            .ToListAsync(ct);

        return ToolSchemas.Serialize(new { matches = rows });
    }

    private async Task<string> GetMyCapacity(ApplicationDbContext db, ChatToolContext ctx, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(ctx.UserId);
        if (user is null)
        {
            return ToolSchemas.Serialize(new { error = "Supervisor account not found." });
        }

        var currentLoad = await db.Matches
            .AsNoTracking()
            .CountAsync(m => m.SupervisorId == ctx.UserId, ct);

        return ToolSchemas.Serialize(new
        {
            maxCapacity = user.MaxProjectCapacity,
            currentLoad,
            available = Math.Max(0, user.MaxProjectCapacity - currentLoad),
            atCapacity = currentLoad >= user.MaxProjectCapacity,
        });
    }
}
