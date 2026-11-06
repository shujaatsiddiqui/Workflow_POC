using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.Activities.Flowchart.Attributes;

namespace ElsaServer.Activities;

/// <summary>
/// Validates payment against multiple business rules:
/// - Balance must be greater than zero
/// - Status must not be 93 (account blocked)
/// - Amount must not exceed balance (no overpayments)
/// - Account must not be closed to posting
/// </summary>
[Activity(
    Namespace = "SFS",
    DisplayName = "ValidatePayment",
    Description = "Validate payment against business rules")]
[FlowNode("Valid", "Invalid")]
public class ValidatePaymentActivity : Activity
{
    [Input(
        DisplayName = "Payment ID",
        Description = "Payment identifier")]
    public Input<string> PaymentId { get; set; } = default!;

    [Input(
        DisplayName = "Amount",
        Description = "Payment amount to validate")]
    public Input<decimal> Amount { get; set; } = default!;

    [Input(
        DisplayName = "Account Balance",
        Description = "Current account balance")]
    public Input<decimal> Balance { get; set; } = default!;

    [Input(
        DisplayName = "Account Status",
        Description = "Account status code (FIC_CLSFD_CD)")]
    public Input<int> Status { get; set; } = default!;

    [Input(
        DisplayName = "Closed To Posting",
        Description = "Closed to posting flag (0=open, 1=closed)")]
    public Input<int> ClosedToPosting { get; set; } = default!;

    [Output(Description = "True if payment passed all validations")]
    public Output<bool> IsValid { get; set; } = default!;

    [Output(Description = "List of validation error messages")]
    public Output<List<string>> Errors { get; set; } = default!;

    [Output(Description = "Payment type: Standard, Payoff, or Overpayment")]
    public Output<string> PaymentType { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Get all inputs
        var paymentId = context.Get(PaymentId);
        var amount = context.Get(Amount);
        var balance = context.Get(Balance);
        var status = context.Get(Status);
        var closedToPosting = context.Get(ClosedToPosting);

        var errors = new List<string>();

        // Validation 1: Balance must not be zero
        if (balance <= 0)
        {
            errors.Add("Account balance is zero or negative");
        }

        // Validation 2: Status must not be 93 (blocked account)
        if (status == 93)
        {
            errors.Add("Account status 93 blocks posting");
        }

        // Validation 3: Account must not be closed to posting
        if (closedToPosting == 1)
        {
            errors.Add("Account is closed to posting");
        }

        // Validation 4: Amount validation and payment type determination
        string paymentType;
        if (amount < balance)
        {
            paymentType = "Standard";
        }
        else if (amount == balance)
        {
            paymentType = "Payoff";
        }
        else
        {
            paymentType = "Overpayment";
            errors.Add($"Payment amount ${amount} exceeds balance ${balance}");
        }

        // Set outputs
        var isValid = errors.Count == 0;
        context.Set(IsValid, isValid);
        context.Set(Errors, errors);
        context.Set(PaymentType, paymentType);

        // Complete with appropriate outcome
        var outcome = isValid ? "Valid" : "Invalid";
        await context.CompleteActivityWithOutcomesAsync(outcome);
    }
}
