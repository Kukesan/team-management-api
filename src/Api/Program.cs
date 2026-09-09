using System.Text;
using System.Text.Json.Serialization;
using Api.Filters;
using Api.Middleware;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Common.Settings;
using Application.Features.Ai;
using Application.Features.Auth;
using Application.Features.Auth.Validators;
using Application.Features.Dashboard;
using Application.Features.Projects;
using Application.Features.Reports;
using Application.Features.Users;
using Domain.Entities;
using FluentValidation;
using Infrastructure.Auth;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicyName = "FrontendCors";

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(options =>
{
    // Enums as strings ("High", "InProgress") both ways — matches ReviewRequest.Action
    // and keeps the API self-describing instead of exposing raw enum ints.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// Route malformed/unbindable request bodies through the same {title, status, errors}
// shape as ValidationFilter/ExceptionHandlingMiddleware, instead of the default ProblemDetails.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new ApiErrorResponse
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Errors = errors
        });
    };
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Paste only the JWT (no 'Bearer ' prefix — Swagger adds it)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. Set it via 'dotnet user-secrets' " +
        "(Development) or the ConnectionStrings__DefaultConnection environment variable (other environments).");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

builder.Services.AddDataProtection();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Kept in sync with RegisterRequestValidator, which is the single source of
        // truth for password rules surfaced to API clients.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.Configure<AiServiceSettings>(builder.Configuration.GetSection(AiServiceSettings.SectionName));
builder.Services.AddHttpClient(AiService.HttpClientName, (sp, client) =>
{
    var aiSettings = sp.GetRequiredService<IOptions<AiServiceSettings>>().Value;
    if (!string.IsNullOrWhiteSpace(aiSettings.BaseUrl))
    {
        client.BaseAddress = new Uri(aiSettings.BaseUrl);
    }
    // Aligned with team-management-ai's own per-call OpenAI budget (services/llm.py,
    // services/help.py: 20s per completion, 2 attempts, short backoff). /chat's tool loop
    // can run up to 6 iterations, so this must comfortably exceed that realistic worst
    // case; 120s still fails a truly hung request rather than waiting on the SDK's 600s
    // default. AiService.PostAsync also applies its own bounded retry on top of this.
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<IAiService, AiService>();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");
if (string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured. Set it via 'dotnet user-secrets' (Development) or the Jwt__Key environment variable.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await DbInitializer.SeedRolesAsync(roleManager);

    if (app.Environment.IsDevelopment())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await DbInitializer.SeedDevAdminAsync(userManager, app.Configuration, logger);
        await DbInitializer.SeedDemoUsersAndProjectsAsync(userManager, dbContext, logger);
        await DbInitializer.SeedReportsAsync(userManager, dbContext, logger);
    }
}

app.Run();

public partial class Program { }
