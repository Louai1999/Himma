using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Himma.Api.Data;
using Himma.Api.Features.Account;
using Himma.Api.Features.Admin;
using Himma.Api.Features.History;
using Himma.Api.Features.Supplements;
using Himma.Api.Infrastructure.Push;
using Himma.Api.Infrastructure.Audit;
using Himma.Api.Features.Workouts;
using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.Database;
using Himma.Api.Infrastructure.Errors;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Identity;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.RateLimiting;
using Himma.Api.Infrastructure.Security;
using Himma.Api.Infrastructure.Validation;
using Himma.Api.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOptions<AdminOptions>().Bind(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddOptions<AdherenceOptions>()
    .Bind(builder.Configuration.GetSection(AdherenceOptions.SectionName))
    .Validate(o => o.CalorieTolerancePercent is > 0 and <= 50
        && o.ProteinMinPercent is > 0 and <= 100
        && o.WaterMinPercent is > 0 and <= 100, "Adherence percentages are out of range.")
    .ValidateOnStart();

builder.Services.AddHimmaDatabase(builder.Configuration, builder.Environment);
builder.Services.AddHimmaIdentity();
builder.Services.AddHimmaAuthentication(builder.Configuration);
builder.Services.AddHimmaRateLimiting(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<LastPerformanceQuery>();
builder.Services.AddScoped<PlanService>();

builder.Services.AddOptions<AccountDeletionOptions>().Bind(builder.Configuration.GetSection(AccountDeletionOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddScoped<PasswordReauthentication>();
builder.Services.AddScoped<AccountLifecycleService>();
builder.Services.AddScoped<UserDataExporter>();
builder.Services.AddScoped<AdminMetricsService>();
builder.Services.AddHostedService<ScheduledPurgeService>();

// Supplements and reminders (Web Push).
builder.Services.AddDataProtection();
builder.Services.AddOptions<PushOptions>().Bind(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.AddOptions<ReminderOptions>().Bind(builder.Configuration.GetSection(ReminderOptions.SectionName));
builder.Services.AddSingleton<PushKeyStore>();
builder.Services.AddSingleton<TakeTokens>();
builder.Services.AddScoped<SupplementIntakeService>();
builder.Services.AddHttpClient<IPushSender, WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<ReminderService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ReminderService>());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        // FluentValidation gives the (Arabic) "required" messages instead of MVC's built-in ones.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        // Enums travel as camelCase strings; numbers or unknown names are rejected.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        // Unknown fields (for example "role") are rejected instead of silently ignored.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}
else
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });
}

builder.AddHimmaHosting();

var app = builder.Build();

// Deploy step: "dotnet Himma.Api.dll --migrate" applies pending migrations, seeds, and exits.
if (args.Contains("--migrate"))
{
    await app.Services.InitializeDatabaseAsync(applyMigrations: true);
    return;
}

await app.Services.InitializeDatabaseAsync();

// Real client IP and scheme when running behind the hosting platform's proxy.
app.UseHimmaForwardedHeaders();
// Early, so error pages and rate-limit messages are also in the user's language.
app.UseHimmaLocalization();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// The hosting platform's own health check calls /health over plain HTTP inside its network; everything else is HTTPS-only.
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseHttpsRedirection());
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // The frontend has no build step (no hashed file names), so browsers must revalidate
    // (cheap, via ETag) to pick up a new version right after a deploy.
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication runs before the rate limiter so limits can be per user, not just per IP.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Lets the test project start the app with WebApplicationFactory<Program>.
public partial class Program;
