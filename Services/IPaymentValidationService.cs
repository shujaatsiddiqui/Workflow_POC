namespace ElsaServer.Services;

/// <summary>
/// Service for validating payments against SFS business rules.
/// Interface allows swapping mock implementation with real SFS API later.
/// </summary>
public interface IPaymentValidationService
{
    /// <summary>
    /// Gets the list of available validation rules
    /// </summary>
    /// <returns>List of rule names (e.g., "BR-005", "BR-008")</returns>
    List<string> GetAvailableRules();

    /// <summary>
    /// Validates a payment against business rules
    /// </summary>
    /// <param name="paymentId">Payment identifier (e.g., PAY-001)</param>
    /// <param name="ruleName">Optional: specific rule to run (e.g., "BR-005"), or null for all rules</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with pass/fail and any error messages</returns>
    Task<PaymentValidationResult> ValidateAsync(string paymentId, string? ruleName = null, CancellationToken cancellationToken = default);
}
