using Divvy.Api.Configuration;
using Divvy.Api.Data;
using Divvy.Api.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Npgsql 6+ maps DateTime to 'timestamp with time zone' and rejects DateTimeKind.Unspecified.
// This switch preserves SQL Server–compatible behaviour (timestamp without time zone,
// accepts Unspecified DateTimes) without requiring changes to business logic.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── User secrets (always loaded so local prod-mode testing works) ────────────
// ASP.NET Core only loads user-secrets in Development by default.
// Adding them unconditionally (optional: true) allows running with
// ASPNETCORE_ENVIRONMENT=Production locally without losing dotnet user-secrets.
builder.Configuration.AddUserSecrets<Program>(optional: true);

// ── Infisical (production only) ─────────────────────────────────────────────
// builder.Configuration already has appsettings.json, appsettings.Production.json,
// and environment variables loaded by WebApplication.CreateBuilder.
// Infisical:ProjectId lives in appsettings.json; Infisical:ClientId /
// Infisical:ClientSecret come from systemd env vars (Infisical__ClientId etc.)
if (builder.Environment.IsProduction())
{
    if (!string.IsNullOrEmpty(builder.Configuration["Infisical:ClientId"]))
    {
        var envSlug = builder.Configuration["Infisical:EnvironmentSlug"] ?? "prod";
        builder.Configuration.AddInfisical(builder.Configuration, envSlug);
    }
    else
    {
        Console.WriteLine("[Infisical] Infisical:ClientId not set — falling back to appsettings.");
    }
}

// Add services with environment-specific configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly("Divvy.Api");
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    })
    .UseSnakeCaseNamingConvention();

    // Enable detailed errors in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Configure JWT Authentication with environment-specific keys
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ??
                     builder.Configuration["Jwt:Key"] ??
                     throw new InvalidOperationException("JWT Key not configured");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateIssuer = builder.Environment.IsProduction(),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = builder.Environment.IsProduction(),
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            RoleClaimType = "role",
            NameClaimType = "email",
            ClockSkew = TimeSpan.Zero
        };
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuth");

                var name = ctx.Principal?.Identity?.Name ?? "(no name)";
                var roles = ctx.Principal?.Claims
                    .Where(c => c.Type == "role")
                    .Select(c => c.Value)
                    .ToList() ?? new List<string>();

                logger.LogInformation("JWT validated. Name={Name}. Roles={Roles}.",
                    name,
                    roles.Count == 0 ? "(none)" : string.Join(",", roles));

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuth");

                logger.LogError(ctx.Exception, "JWT authentication failed.");
                return Task.CompletedTask;
            },
            OnForbidden = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuth");

                var endpoint = ctx.HttpContext.GetEndpoint();
                var claims = string.Join(", ", ctx.HttpContext.User.Claims.Select(c => $"{c.Type}={c.Value}"));

                logger.LogWarning("403 Forbidden. Path={Path}. Endpoint={Endpoint}. User={User}. Claims={Claims}",
                    ctx.HttpContext.Request.Path,
                    endpoint?.DisplayName ?? "(unknown endpoint)",
                    ctx.HttpContext.User.Identity?.Name ?? "(anonymous)",
                    claims);

                return Task.CompletedTask;
            }
        };
    });

// Configure authorization with role-based policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("AdminOrAbove",   p => p.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("MemberOrAbove",  p => p.RequireRole("SuperAdmin", "Admin", "Member"));
});

// Environment-specific CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Dev origins are configured in appsettings.Development.json under Cors:AllowedOrigins.
            // Add ngrok/devtunnel URLs there — no code change or rebuild required.
            var devOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            policy.WithOrigins(devOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Production CORS - configured in appsettings.Production.json under Cors:AllowedOrigins
            var prodOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            policy.WithOrigins(prodOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Add Model State validation
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = false;
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // send/receive enums as their string names (e.g. "AboutTo") so the
        // Angular client can post readable values instead of magic numbers.
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // maintain camel-case property naming (matching what Angular expects).
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
builder.Services.AddScoped<AppConfigService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<EmailTemplateService>();
builder.Services.AddScoped<MfaService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NotificationPreferenceService>();
builder.Services.AddScoped<UserDeviceService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TimeZoneService>();
builder.Services.AddScoped<ExpenseCycleService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<IGroupService, GroupService>();

// WebAuthn / Biometric authentication
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFido2>(_ =>
{
    var webAuthnConfig = builder.Configuration.GetSection("WebAuthn");
    return new Fido2(new Fido2Configuration
    {
        ServerDomain = webAuthnConfig["RelyingPartyId"] ?? "localhost",
        ServerName   = webAuthnConfig["RelyingPartyName"] ?? "Divvy",
        Origins      = new HashSet<string>
        {
            webAuthnConfig["Origin"] ?? "https://localhost:4200"
        },
        TimestampDriftTolerance = 300_000  // 5 minutes (milliseconds)
    });
});
builder.Services.AddScoped<WebAuthnService>();

// Push notification services
builder.Services.Configure<VapidSettings>(builder.Configuration.GetSection("Vapid"));
builder.Services.AddHttpClient<PushNotificationSender>();
builder.Services.AddScoped<IPushNotificationSender, PushNotificationSender>();
builder.Services.AddHostedService<BgTimerHostedService>();

// Add Swagger/OpenAPI (only in development)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Divvy API", Version = "v1" });
    });
}

var app = builder.Build();

// Environment-specific database initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var passwordHashingService = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();

    if (app.Environment.IsDevelopment())
    {
        await context.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(context, passwordHashingService);
    }
    else
    {
        await context.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(context, passwordHashingService);
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Divvy API V1");
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();

// HTTPS redirection (important for production)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowedOrigins");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
else
{
    app.MapGet("/", () => Results.Content("""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>Divvy API</title>
            <style>
                body { font-family: sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #f5f5f5; }
                .card { background: white; padding: 2rem 3rem; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); text-align: center; }
                h1 { margin: 0 0 0.5rem; color: #333; }
                p { color: #666; margin: 0; }
            </style>
        </head>
        <body>
            <div class="card">
                <h1>Divvy API</h1>
                <p>Service is running.</p>
            </div>
        </body>
        </html>
        """, "text/html")).ExcludeFromDescription();
}

app.MapControllers();
app.Run();
