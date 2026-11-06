using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using ElsaServer.Services;

namespace ElsaServer.Activities;

/// <summary>
/// Custom activity for validating payments against SFS business rules.
/// Business users drag this onto the canvas and only need to provide a Payment ID.
/// All technical details (API calls, rules, etc.) are hidden.
/// </summary>
[Activity(
    Namespace = "SFS",
    DisplayName = "SFS_ValidatePayment",
    Description = "Validates a payment against SFS business rules.")]
public class SfsValidatePaymentActivity : Activity
{
    /// <summary>
    /// Payment ID to validate (e.g., PAY-001)
    /// </summary>
    [Input(
        DisplayName = "Payment ID",
        Description = "The payment identifier to validate (e.g., PAY-001)")]
    public Input<string> PaymentId { get; set; } = default!;

    /// <summary>
    /// Which validation rule(s) to run
    /// </summary>
    [Input(
        DisplayName = "Rule Selection",
        Description = "Select which rule to run, or 'All Rules' to run all validations",
        DefaultValue = "All Rules")]
    public Input<string> RuleSelection { get; set; } = new("All Rules");

    /// <summary>
    /// Output: Whether the payment passed validation
    /// </summary>
    [Output(
        DisplayName = "Is Valid",
        Description = "True if payment passed all validation rules")]
    public Output<bool> IsValid { get; set; } = default!;

    /// <summary>
    /// Output: List of validation error messages (empty if valid)
    /// </summary>
    [Output(
        DisplayName = "Errors",
        Description = "List of validation error messages")]
    public Output<List<string>> Errors { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Get inputs
        var paymentId = PaymentId.Get(context);
        var ruleSelection = RuleSelection.Get(context) ?? "All Rules";

        // Validate input
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            // Set error outputs
            IsValid.Set(context, false);
            Errors.Set(context, new List<string> { "Payment ID is required" });
            await context.CompleteActivityAsync();
            return;
        }

        try
        {
            // Get validation service from DI
            var validationService = context.GetRequiredService<IPaymentValidationService>();

            // Determine which rule to run
            string? ruleName = ruleSelection == "All Rules" ? null : ruleSelection;

            // Call the validation service
            var result = await validationService.ValidateAsync(paymentId, ruleName, context.CancellationToken);

            // Set outputs
            IsValid.Set(context, result.IsValid);
            Errors.Set(context, result.Errors);

            // Complete with Done outcome so the workflow continues
            await context.CompleteActivityAsync();
        }
        catch (Exception ex)
        {
            // Handle any errors gracefully - don't expose technical details to business users
            IsValid.Set(context, false);
            Errors.Set(context, new List<string> { $"Validation error: {ex.Message}" });
            await context.CompleteActivityAsync();
        }
    }

}
