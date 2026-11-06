namespace ElsaServer.Models;

/// <summary>
/// Mock payment data structure for POC.
/// In production, this would come from the real SFS API.
/// </summary>
public class PaymentData
{
    public string PaymentId { get; set; } = string.Empty;
    public decimal CreditAmount { get; set; }
    public int FIC_CLSFD_CD { get; set; } // Account status code
}
