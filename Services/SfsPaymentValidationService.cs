using ElsaServer.Models;
using RulesEngine.Models;
using System.Text.Json;

namespace ElsaServer.Services;

/// <summary>
/// Mock implementation of payment validation service for POC.
/// Uses Microsoft RulesEngine to evaluate business rules from JSON.
/// Can be replaced with real SFS API client later without changing the activity.
/// </summary>
public class SfsPaymentValidationService : IPaymentValidationService
{
    private readonly RulesEngine.RulesEngine _rulesEngine;
    private readonly Dictionary<string, PaymentData> _mockPayments;
    private readonly List<string> _availableRules;

    public SfsPaymentValidationService(IWebHostEnvironment environment)
    {
        // Mock payment data (in-memory for POC)
        _mockPayments = new Dictionary<string, PaymentData>
        {
            { "PAY-001", new PaymentData { PaymentId = "PAY-001", CreditAmount = 1000, FIC_CLSFD_CD = 10 } },
            { "PAY-002", new PaymentData { PaymentId = "PAY-002", CreditAmount = 0, FIC_CLSFD_CD = 10 } },
            { "PAY-003", new PaymentData { PaymentId = "PAY-003", CreditAmount = 1000, FIC_CLSFD_CD = 93 } },
            { "PAY-004", new PaymentData { PaymentId = "PAY-004", CreditAmount = 0, FIC_CLSFD_CD = 93 } }
        };

        // Load validation rules from JSON file
        var rulesFilePath = Path.Combine(environment.ContentRootPath, "Rules", "ValidationRules.json");
        var rulesJson = File.ReadAllText(rulesFilePath);
        var workflows = JsonSerializer.Deserialize<List<Workflow>>(rulesJson);

        _rulesEngine = new RulesEngine.RulesEngine(workflows?.ToArray() ?? Array.Empty<Workflow>());

        // Extract available rule names for UI dropdown
        _availableRules = workflows?
            .SelectMany(w => w.Rules)
            .Select(r => r.RuleName)
            .ToList() ?? new List<string>();
    }

    public List<string> GetAvailableRules() => _availableRules;

    public async Task<PaymentValidationResult> ValidateAsync(string paymentId, string? ruleName = null, CancellationToken cancellationToken = default)
    {
        // Check if payment exists in mock data
        if (!_mockPayments.TryGetValue(paymentId, out var payment))
        {
            return new PaymentValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Payment not found" }
            };
        }

        // Execute validation rules using RulesEngine
        var ruleParameters = new[] { new RuleParameter("input1", payment) };
        var allResults = await _rulesEngine.ExecuteAllRulesAsync("PeritusRetailRemitRules", ruleParameters);

        // Filter results if specific rule requested
        var ruleResults = string.IsNullOrWhiteSpace(ruleName)
            ? allResults
            : allResults.Where(r => r.Rule.RuleName == ruleName).ToList();

        // Collect errors from failed rules
        var errors = ruleResults
            .Where(r => !r.IsSuccess)
            .Select(r => r.Rule.ErrorMessage ?? r.Rule.RuleName)
            .ToList();

        return new PaymentValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
