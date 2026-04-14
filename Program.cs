using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using project_approval_system.Components;
using project_approval_system.Data;
using project_approval_system.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString),
    lifetime: ServiceLifetime.Scoped);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireStudent", p => p.RequireRole(Roles.Student));
    options.AddPolicy("RequireSupervisor", p => p.RequireRole(Roles.Supervisor));
    options.AddPolicy("RequireModuleLeader", p => p.RequireRole(Roles.ModuleLeader));
});

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddMudServices();
builder.Services.AddScoped<IProjectMatchingService, ProjectMatchingService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapPost("/Account/LoginSubmit", async (
    HttpContext httpContext,
    SignInManager<ApplicationUser> signInManager) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var email = GetFormValue(form, "Input.Email", "Email");
    var password = GetFormValue(form, "Input.Password", "Password");
    var returnUrl = httpContext.Request.Query["returnUrl"].ToString();

    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
    {
        var result = await signInManager.PasswordSignInAsync(
            email,
            password,
            isPersistent: false,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return Results.Redirect(GetSafeReturnUrl(returnUrl));
        }

        if (result.IsLockedOut)
        {
            return Results.Redirect(GetLoginRedirect("locked", returnUrl));
        }
    }

    return Results.Redirect(GetLoginRedirect("invalid", returnUrl));
}).WithMetadata(new RequireAntiforgeryTokenAttribute(true));

app.MapPost("/Account/LogoutSubmit", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
})
.RequireAuthorization()
.WithMetadata(new RequireAntiforgeryTokenAttribute(true));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await DbInitializer.SeedAsync(app.Services);

app.Run();

static string GetFormValue(IFormCollection form, string fullName, string fallbackName)
{
    if (form.TryGetValue(fullName, out var value))
    {
        return value.ToString();
    }

    return form[fallbackName].ToString();
}

static string GetLoginRedirect(string error, string? returnUrl)
{
    var path = $"/Account/Login?error={Uri.EscapeDataString(error)}";
    var safeReturnUrl = GetSafeReturnUrl(returnUrl);

    return safeReturnUrl == "/"
        ? path
        : $"{path}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
}

static string GetSafeReturnUrl(string? returnUrl)
{
    return string.IsNullOrWhiteSpace(returnUrl) || !IsLocalUrl(returnUrl)
        ? "/"
        : returnUrl;
}

static bool IsLocalUrl(string url)
{
    if (url[0] != '/')
    {
        return false;
    }

    return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
}
