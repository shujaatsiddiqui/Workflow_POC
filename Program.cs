using Elsa.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Elsa.Persistence.EFCore.Extensions;
using ElsaServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Elsa services with SQL Server persistence
var connectionString = "Server=localhost,1433;Database=ElsaDb;User Id=sa;Password=Elsa@12345;TrustServerCertificate=True;MultipleActiveResultSets=true;";

builder.Services.AddElsa(elsa =>
{
    // Workflow Management persistence (stores workflow definitions and instances)
    elsa.UseWorkflowManagement(management =>
    {
        management.UseEntityFrameworkCore(ef =>
        {
            ef.UseSqlServer(connectionString);
            ef.RunMigrations = true; // Auto-apply migrations on startup
        });
    });

    // Workflow Runtime persistence (stores bookmarks, inbox, execution logs)
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseEntityFrameworkCore(ef =>
        {
            ef.UseSqlServer(connectionString);
            ef.RunMigrations = true;
        });
    });

    // Identity for local admin login to Studio
    elsa.UseIdentity(identity =>
    {
        identity.TokenOptions = options => options.SigningKey = builder.Configuration["Identity:Tokens:SigningKey"]!;
        identity.UseDefaultAdmin(admin => admin
            .WithAdminUserName(builder.Configuration["Identity:Bootstrap:UserName"]!)
            .WithAdminPassword(builder.Configuration["Identity:Bootstrap:Password"]!)
            .WithAdminRoleName("admin")
            .WithAdminRolePermissions(new List<string> { "*" })
        );
    });

    elsa.UseDefaultAuthentication();

    // APIs and features
    elsa.UseWorkflowsApi();
    elsa.UseDashboardApi();
    elsa.UseWorkflowRuntimeDashboard();
    elsa.UseRealTimeWorkflows();

    // Expression languages
    elsa.UseJavaScript();
    elsa.UseCSharp();

    // HTTP activities
    elsa.UseHttp(http =>
    {
        http.ConfigureHttpOptions = options =>
        {
            options.BaseUrl = new Uri("http://localhost:5001");
        };
    });

    // Scheduling
    elsa.UseScheduling();

    // Register custom activities from this assembly
    elsa.AddActivitiesFrom<Program>();
});

// Register payment validation service
builder.Services.AddSingleton<IPaymentValidationService, SfsPaymentValidationService>();

// CORS to allow Studio to connect
builder.Services.AddCors(cors =>
{
    cors.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("x-elsa-workflow-instance-id"));
});

var app = builder.Build();

// Quick test of payment validation service
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var validationService = app.Services.GetRequiredService<IPaymentValidationService>();

logger.LogInformation("===== Payment Validation Service Test =====");
logger.LogInformation("Available rules: {Rules}", string.Join(", ", validationService.GetAvailableRules()));

logger.LogInformation("--- Testing All Rules ---");
foreach (var paymentId in new[] { "PAY-001", "PAY-002", "PAY-003", "PAY-004" })
{
    var result = await validationService.ValidateAsync(paymentId);
    logger.LogInformation("{PaymentId}: IsValid={IsValid}, Errors=[{Errors}]",
        paymentId, result.IsValid, string.Join(", ", result.Errors));
}

logger.LogInformation("--- Testing BR-005 Only (Zero Amount) ---");
var result005 = await validationService.ValidateAsync("PAY-004", "BR-005");
logger.LogInformation("PAY-004 with BR-005 only: IsValid={IsValid}, Errors=[{Errors}]",
    result005.IsValid, string.Join(", ", result005.Errors));

logger.LogInformation("--- Testing BR-008 Only (Status 93) ---");
var result008 = await validationService.ValidateAsync("PAY-004", "BR-008");
logger.LogInformation("PAY-004 with BR-008 only: IsValid={IsValid}, Errors=[{Errors}]",
    result008.IsValid, string.Join(", ", result008.Errors));

logger.LogInformation("============================================");

// Middleware pipeline
app.UseCors();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseWorkflowsApi();
app.UseWorkflows();
app.UseWorkflowsSignalRHubs();

app.Run();
