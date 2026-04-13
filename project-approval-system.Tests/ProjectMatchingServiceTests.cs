using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using project_approval_system.Data;
using project_approval_system.Services;

namespace project_approval_system.Tests;

public sealed class ProjectMatchingServiceTests
{
    [Fact]
    public async Task ExpressInterest_sets_proposal_under_review()
    {
        await using var app = await TestApp.CreateAsync();
        var proposal = await app.CreateProposalAsync();

        var result = await app.MatchingService.ExpressInterestAsync(app.Supervisor.Id, proposal.Id);

        Assert.True(result.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        var savedProposal = await db.Proposals.Include(p => p.Interests).SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal(ProposalStatus.UnderReview, savedProposal.Status);
        Assert.Single(savedProposal.Interests);
    }

    [Fact]
    public async Task ExpressInterest_rejects_proposal_outside_supervisor_expertise()
    {
        await using var app = await TestApp.CreateAsync();
        var supervisor = await app.CreateUserAsync("outside@pas.local", Roles.Supervisor, grantDefaultExpertise: false);
        var proposal = await app.CreateProposalAsync();

        var result = await app.MatchingService.ExpressInterestAsync(supervisor.Id, proposal.Id);

        Assert.False(result.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        var savedProposal = await db.Proposals.Include(p => p.Interests).SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal(ProposalStatus.Pending, savedProposal.Status);
        Assert.Empty(savedProposal.Interests);
    }

    [Fact]
    public async Task Duplicate_interest_is_rejected()
    {
        await using var app = await TestApp.CreateAsync();
        var proposal = await app.CreateProposalAsync();

        Assert.True((await app.MatchingService.ExpressInterestAsync(app.Supervisor.Id, proposal.Id)).Succeeded);
        var duplicate = await app.MatchingService.ExpressInterestAsync(app.Supervisor.Id, proposal.Id);

        Assert.False(duplicate.Succeeded);
    }

    [Fact]
    public async Task Confirm_requires_existing_interest()
    {
        await using var app = await TestApp.CreateAsync();
        var proposal = await app.CreateProposalAsync();

        var result = await app.MatchingService.ConfirmMatchAsync(app.Supervisor.Id, proposal.Id);

        Assert.False(result.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        Assert.Empty(await db.Matches.ToListAsync());
    }

    [Fact]
    public async Task Confirm_rejects_proposal_outside_supervisor_expertise()
    {
        await using var app = await TestApp.CreateAsync();
        var supervisor = await app.CreateUserAsync("outside@pas.local", Roles.Supervisor, grantDefaultExpertise: false);
        var proposal = await app.CreateProposalAsync();

        await using (var setupDb = await app.CreateDbContextAsync())
        {
            setupDb.SupervisorInterests.Add(new SupervisorInterest
            {
                ProposalId = proposal.Id,
                SupervisorId = supervisor.Id,
                ExpressedAt = DateTime.UtcNow,
            });
            await setupDb.SaveChangesAsync();
        }

        var result = await app.MatchingService.ConfirmMatchAsync(supervisor.Id, proposal.Id);

        Assert.False(result.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        Assert.Empty(await db.Matches.ToListAsync());
        Assert.Equal(ProposalStatus.Pending, await db.Proposals.Where(p => p.Id == proposal.Id).Select(p => p.Status).SingleAsync());
    }

    [Fact]
    public async Task First_confirmed_supervisor_wins()
    {
        await using var app = await TestApp.CreateAsync();
        var secondSupervisor = await app.CreateUserAsync("second@pas.local", Roles.Supervisor);
        var proposal = await app.CreateProposalAsync();

        Assert.True((await app.MatchingService.ExpressInterestAsync(app.Supervisor.Id, proposal.Id)).Succeeded);
        Assert.True((await app.MatchingService.ExpressInterestAsync(secondSupervisor.Id, proposal.Id)).Succeeded);

        var first = await app.MatchingService.ConfirmMatchAsync(app.Supervisor.Id, proposal.Id);
        var second = await app.MatchingService.ConfirmMatchAsync(secondSupervisor.Id, proposal.Id);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);

        await using var db = await app.CreateDbContextAsync();
        var match = await db.Matches.SingleAsync();
        Assert.Equal(app.Supervisor.Id, match.SupervisorId);
    }

    [Fact]
    public async Task Capacity_blocks_confirmed_match()
    {
        await using var app = await TestApp.CreateAsync(supervisorCapacity: 1);
        var firstProposal = await app.CreateProposalAsync(title: "First");
        var secondProposal = await app.CreateProposalAsync(title: "Second");

        Assert.True((await app.MatchingService.ExpressInterestAsync(app.Supervisor.Id, firstProposal.Id)).Succeeded);
        Assert.True((await app.MatchingService.ConfirmMatchAsync(app.Supervisor.Id, firstProposal.Id)).Succeeded);

        Assert.True((await app.MatchingService.ExpressInterestAsync(app.Supervisor.Id, secondProposal.Id)).Succeeded);
        var second = await app.MatchingService.ConfirmMatchAsync(app.Supervisor.Id, secondProposal.Id);

        Assert.False(second.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        Assert.Single(await db.Matches.ToListAsync());
    }

    [Fact]
    public async Task ExpressInterest_rejects_existing_match_without_changing_status()
    {
        await using var app = await TestApp.CreateAsync();
        var proposal = await app.CreateProposalAsync(status: ProposalStatus.Matched);

        await using (var setupDb = await app.CreateDbContextAsync())
        {
            setupDb.Matches.Add(new Match
            {
                ProposalId = proposal.Id,
                SupervisorId = app.Supervisor.Id,
                ConfirmedAt = DateTime.UtcNow,
            });
            await setupDb.SaveChangesAsync();
        }

        var result = await app.MatchingService.ExpressInterestAsync(app.Supervisor.Id, proposal.Id);

        Assert.False(result.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        var savedProposal = await db.Proposals.Include(p => p.Interests).Include(p => p.Match).SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal(ProposalStatus.Matched, savedProposal.Status);
        Assert.Empty(savedProposal.Interests);
        Assert.NotNull(savedProposal.Match);
    }

    [Fact]
    public async Task Module_leader_can_assign_unmatched_proposal()
    {
        await using var app = await TestApp.CreateAsync();
        var proposal = await app.CreateProposalAsync();

        var result = await app.MatchingService.AdminAssignAsync(app.ModuleLeader.Id, proposal.Id, app.Supervisor.Id);

        Assert.True(result.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        var savedProposal = await db.Proposals.Include(p => p.Match).SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal(ProposalStatus.Matched, savedProposal.Status);
        Assert.NotNull(savedProposal.Match);
    }

    [Fact]
    public async Task Module_leader_cannot_reassign_matched_proposal()
    {
        await using var app = await TestApp.CreateAsync();
        var secondSupervisor = await app.CreateUserAsync("second@pas.local", Roles.Supervisor);
        var proposal = await app.CreateProposalAsync();

        Assert.True((await app.MatchingService.AdminAssignAsync(app.ModuleLeader.Id, proposal.Id, app.Supervisor.Id)).Succeeded);
        var reassign = await app.MatchingService.AdminAssignAsync(app.ModuleLeader.Id, proposal.Id, secondSupervisor.Id);

        Assert.False(reassign.Succeeded);
        await using var db = await app.CreateDbContextAsync();
        var match = await db.Matches.SingleAsync();
        Assert.Equal(app.Supervisor.Id, match.SupervisorId);
    }

    private sealed class TestApp : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly SqliteConnection connection;

        private TestApp(ServiceProvider provider, SqliteConnection connection)
        {
            this.provider = provider;
            this.connection = connection;
        }

        public ApplicationUser Student { get; private set; } = null!;
        public ApplicationUser Supervisor { get; private set; } = null!;
        public ApplicationUser ModuleLeader { get; private set; } = null!;
        public ResearchArea ResearchArea { get; private set; } = null!;

        public IProjectMatchingService MatchingService =>
            provider.GetRequiredService<IProjectMatchingService>();

        public static async Task<TestApp> CreateAsync(int supervisorCapacity = ApplicationUser.DefaultMaxProjectCapacity)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(connection);
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
                options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
            services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
                options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
            services
                .AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IProjectMatchingService, ProjectMatchingService>();

            var provider = services.BuildServiceProvider();
            var app = new TestApp(provider, connection);

            await using var db = await app.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();

            foreach (var role in Roles.All)
            {
                db.Roles.Add(new IdentityRole(role) { NormalizedName = role.ToUpperInvariant() });
            }

            app.ResearchArea = new ResearchArea { Name = "Artificial Intelligence" };
            db.ResearchAreas.Add(app.ResearchArea);
            await db.SaveChangesAsync();

            app.Student = await app.CreateUserAsync("student@pas.local", Roles.Student);
            app.Supervisor = await app.CreateUserAsync("supervisor@pas.local", Roles.Supervisor, supervisorCapacity);
            app.ModuleLeader = await app.CreateUserAsync("lead@pas.local", Roles.ModuleLeader);

            return app;
        }

        public async Task<ApplicationDbContext> CreateDbContextAsync()
        {
            var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            return await factory.CreateDbContextAsync();
        }

        public async Task<ApplicationUser> CreateUserAsync(
            string email,
            string role,
            int capacity = ApplicationUser.DefaultMaxProjectCapacity,
            bool grantDefaultExpertise = true)
        {
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = email.Split('@')[0],
                ContactEmail = email,
                MaxProjectCapacity = capacity,
            };

            var create = await userManager.CreateAsync(user, "Password!123");
            Assert.True(create.Succeeded, string.Join(" ", create.Errors.Select(e => e.Description)));

            var addRole = await userManager.AddToRoleAsync(user, role);
            Assert.True(addRole.Succeeded, string.Join(" ", addRole.Errors.Select(e => e.Description)));

            if (role == Roles.Supervisor && grantDefaultExpertise)
            {
                await using var db = await CreateDbContextAsync();
                db.SupervisorExpertise.Add(new SupervisorExpertise
                {
                    SupervisorId = user.Id,
                    ResearchAreaId = ResearchArea.Id,
                });
                await db.SaveChangesAsync();
            }

            return user;
        }

        public async Task<ProjectProposal> CreateProposalAsync(
            string title = "AI scheduling assistant",
            ProposalStatus status = ProposalStatus.Pending)
        {
            await using var db = await CreateDbContextAsync();
            var proposal = new ProjectProposal
            {
                Title = title,
                Abstract = "A proposal for a blind-matched project.",
                TechnicalStack = "ASP.NET Core, SQL Server",
                GroupMembers = "Second Student <second@pas.local>",
                ResearchAreaId = ResearchArea.Id,
                OwnerUserId = Student.Id,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            db.Proposals.Add(proposal);
            await db.SaveChangesAsync();
            return proposal;
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
