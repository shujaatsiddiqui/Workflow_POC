using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.Activities.Flowchart.Attributes;

namespace ElsaServer.Activities;

/// <summary>
/// Checks if a payment is a duplicate based on Payment ID and Amount.
/// Compares against previously processed payments.
/// </summary>
[Activity(
    Namespace = "SFS",
    DisplayName = "CheckDuplicate",
    Description = "Check if payment is a duplicate")]
[FlowNode("Duplicate", "NotDuplicate")]
public class CheckDuplicateActivity : Activity
{
    // Mock list of previously posted payments (PaymentId:Amount)
    private static readonly HashSet<string> PostedPayments = new()
    {
        "PAY-001:1000",
        "PAY-005:500"
    };

    [Input(
        DisplayName = "Payment ID",
        Description = "The payment identifier to check")]
    public Input<string> PaymentId { get; set; } = default!;

    [Input(
        DisplayName = "Amount",
        Description = "The payment amount")]
    public Input<decimal> Amount { get; set; } = default!;

    [Output(Description = "True if payment is a duplicate")]
    public Output<bool> IsDuplicate { get; set; } = default!;

    [Output(Description = "Duplicate check message")]
    public Output<string> Message { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Get inputs using new syntax
        var paymentId = context.Get(PaymentId);
        var amount = context.Get(Amount);

        // Check for duplicate
        var key = $"{paymentId}:{amount}";
        var isDuplicate = PostedPayments.Contains(key);

        // Set outputs
        context.Set(IsDuplicate, isDuplicate);

        if (isDuplicate)
        {
            context.Set(Message, $"Payment {paymentId} for ${amount} was already posted");
            await context.CompleteActivityWithOutcomesAsync("Duplicate");
        }
        else
        {
            context.Set(Message, "No duplicate found");
            await context.CompleteActivityWithOutcomesAsync("NotDuplicate");
        }
    }
}
