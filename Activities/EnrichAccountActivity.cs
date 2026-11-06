using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace ElsaServer.Activities;

/// <summary>
/// Enriches payment with current account information.
/// Retrieves Balance, Status, and ClosedToPosting flag.
/// </summary>
[Activity(
    Namespace = "SFS",
    DisplayName = "EnrichAccount",
    Description = "Enrich payment with account information")]
public class EnrichAccountActivity : CodeActivity
{
    // Mock account data (PaymentId -> Account Info)
    private static readonly Dictionary<string, (decimal Balance, int Status, int ClosedToPosting)> AccountData = new()
    {
        { "PAY-001", (1000m, 10, 0) },  // Normal account
        { "PAY-002", (500m, 10, 0) },   // Normal account
        { "PAY-003", (1000m, 93, 0) },  // Status 93 - blocked
        { "PAY-004", (100m, 10, 1) },   // Closed to posting
        { "PAY-006", (0m, 10, 0) }      // Zero balance
    };

    [Input(
        DisplayName = "Payment ID",
        Description = "Payment identifier to retrieve account info for")]
    public Input<string> PaymentId { get; set; } = default!;

    [Output(Description = "Current account balance")]
    public Output<decimal> Balance { get; set; } = default!;

    [Output(Description = "Account status code (FIC_CLSFD_CD)")]
    public Output<int> Status { get; set; } = default!;

    [Output(Description = "Closed to posting flag (0=open, 1=closed)")]
    public Output<int> ClosedToPosting { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var paymentId = context.Get(PaymentId);

        // Retrieve account info (mock data)
        if (AccountData.TryGetValue(paymentId, out var accountInfo))
        {
            context.Set(Balance, accountInfo.Balance);
            context.Set(Status, accountInfo.Status);
            context.Set(ClosedToPosting, accountInfo.ClosedToPosting);
        }
        else
        {
            // Default values if payment not found
            context.Set(Balance, 1000m);
            context.Set(Status, 10);
            context.Set(ClosedToPosting, 0);
        }
    }
}
