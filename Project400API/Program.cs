using System.Security.Claims;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Hubs;
using Project400API.Repositories;
using Project400API.Repositories.Interfaces;
using Project400API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureKeyVaultSecrets("keyvault");

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<AppDbContext>("project400db");

builder.Services.AddMemoryCache();
builder.Services.AddControllers();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStoredCredentialRepository, StoredCredentialRepository>();
builder.Services.AddScoped<IDoorRepository, DoorRepository>();
builder.Services.AddScoped<IKeycardRepository, KeycardRepository>();
builder.Services.AddScoped<IUnlockTokenRepository, UnlockTokenRepository>();
builder.Services.AddScoped<IUnlockRequestRepository, UnlockRequestRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<ITailgateAlertRepository, TailgateAlertRepository>();

builder.Services.AddScoped<PasskeyService>();
builder.Services.AddScoped<QRRegistrationService>();
builder.Services.AddScoped<TailgatingService>();
builder.Services.AddHttpClient("AzureVision");
builder.Services.AddSingleton<IoTHubService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IoTHubService>());

var signalRConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
if (!string.IsNullOrEmpty(signalRConnectionString))
{
    builder.Services.AddSignalR().AddAzureSignalR(signalRConnectionString);
}
else
{
    builder.Services.AddSignalR();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["https://www.eunanmurray.ie"];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthentication("AdminCookie")
    .AddCookie("AdminCookie", options =>
    {
        options.Cookie.Name = "P400.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("IsAdmin", "True"));
});

builder.Services.AddFido2(options =>
{
    var fido2Config = builder.Configuration.GetSection("Fido2");
    options.ServerDomain = fido2Config["ServerDomain"] ?? "www.eunanmurray.ie";
    options.ServerName = fido2Config["ServerName"] ?? "Project400 Door Access";
    options.Origins = fido2Config.GetSection("Origins").Get<HashSet<string>>()
        ?? ["https://www.eunanmurray.ie"];
    options.TimestampDriftTolerance = fido2Config.GetValue("TimestampDriftTolerance", 300000);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Run migrations and seed admin user in the background
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully");

        // Seed admin user
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminUsername = config["Bootstrap:AdminUsername"] ?? "admin";
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);
        if (adminUser != null && !adminUser.IsAdmin)
        {
            adminUser.IsAdmin = true;
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Promoted user '{Username}' to admin", adminUsername);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply database migrations or seed admin");
    }
});

app.MapDefaultEndpoints();
app.MapHub<UnlockHub>("/hubs/unlock");
app.MapGet("/", () => Results.Ok("Project400 API is running"));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseCors("AllowWeb");
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
