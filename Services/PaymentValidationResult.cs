namespace ElsaServer.Services;

/// <summary>
/// Result of payment validation with business rules
/// </summary>
public class PaymentValidationResult
{
    /// <summary>
    /// True if all validation rules passed, false otherwise
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of error messages from failed validation rules
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
