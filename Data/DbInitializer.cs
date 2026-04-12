using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace project_approval_system.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var config = provider.GetRequiredService<IConfiguration>();
        var env = provider.GetRequiredService<IWebHostEnvironment>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));

        await SeedModuleLeaderAsync(config, userManager, logger);

        if (env.IsDevelopment())
        {
            await SeedDevStudentAsync(config, userManager, logger);
            await SeedResearchAreasAsync(config, db, logger);
            await SeedDevSupervisorAsync(config, userManager, db, logger);
        }
    }

    private static async Task SeedModuleLeaderAsync(IConfiguration config, UserManager<ApplicationUser> userManager, ILogger logger)
    {
        var email = config["IdentityDefaults:ModuleLeader:Email"];
        var password = config["IdentityDefaults:ModuleLeader:Password"];
        var displayName = config["IdentityDefaults:ModuleLeader:DisplayName"] ?? "Module Leader";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("IdentityDefaults:ModuleLeader email or password missing; skipping ML bootstrap seed.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            ContactEmail = email,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to create ML bootstrap user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, Roles.ModuleLeader);
        logger.LogInformation("Seeded Module Leader {Email}", email);
    }

    private static async Task SeedDevStudentAsync(IConfiguration config, UserManager<ApplicationUser> userManager, ILogger logger)
    {
        var email = config["IdentityDefaults:DevStudent:Email"];
        var password = config["IdentityDefaults:DevStudent:Password"];
        var displayName = config["IdentityDefaults:DevStudent:DisplayName"] ?? "Dev Student";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            ContactEmail = email,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to create dev Student: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, Roles.Student);
        logger.LogInformation("Seeded dev Student {Email}", email);
    }

    private static async Task SeedResearchAreasAsync(IConfiguration config, ApplicationDbContext db, ILogger logger)
    {
        if (await db.ResearchAreas.AnyAsync())
        {
            return;
        }

        var names = config.GetSection("IdentityDefaults:SeedResearchAreas").Get<string[]>();
        if (names is null || names.Length == 0)
        {
            return;
        }

        foreach (var name in names)
        {
            db.ResearchAreas.Add(new ResearchArea { Name = name });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} research areas", names.Length);
    }

    private static async Task SeedDevSupervisorAsync(
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILogger logger)
    {
        var email = config["IdentityDefaults:DevSupervisor:Email"];
        var password = config["IdentityDefaults:DevSupervisor:Password"];
        var displayName = config["IdentityDefaults:DevSupervisor:DisplayName"] ?? "Dev Supervisor";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                ContactEmail = email,
                MaxProjectCapacity = ApplicationUser.DefaultMaxProjectCapacity,
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Failed to create dev Supervisor: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(user, Roles.Supervisor);
            logger.LogInformation("Seeded dev Supervisor {Email}", email);
        }

        var existingExpertise = await db.SupervisorExpertise
            .AnyAsync(e => e.SupervisorId == user.Id);
        if (existingExpertise)
        {
            return;
        }

        var areaNames = config.GetSection("IdentityDefaults:DevSupervisor:Expertise").Get<string[]>()
            ?? ["Artificial Intelligence", "Web Development"];
        var areas = await db.ResearchAreas
            .Where(r => areaNames.Contains(r.Name))
            .ToListAsync();

        foreach (var area in areas)
        {
            db.SupervisorExpertise.Add(new SupervisorExpertise
            {
                SupervisorId = user.Id,
                ResearchAreaId = area.Id,
            });
        }

        await db.SaveChangesAsync();
    }
}
