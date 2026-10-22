using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Himma.Api.Data;
using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.Database;
using Himma.Api.Infrastructure.Errors;
using Himma.Api.Infrastructure.Identity;
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

builder.Services.AddHimmaDatabase(builder.Configuration, builder.Environment);
builder.Services.AddHimmaIdentity();
builder.Services.AddHimmaAuthentication(builder.Configuration);
builder.Services.AddHimmaRateLimiting(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

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

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

app.UseDefaultFiles();
app.UseStaticFiles();

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
