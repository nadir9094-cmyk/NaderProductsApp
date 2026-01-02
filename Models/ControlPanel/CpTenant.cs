using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models.ControlPanel;

public class CpTenant
{
    public decimal AmountDue { get; set; } = 0m;
    public decimal AmountPaid { get; set; } = 0m;

    public decimal Amount { get; set; } = 0m;
    public bool IsPaid { get; set; } = false;
    public int Id { get; set; }

    [Required] public string TenantCode { get; set; } = ""; // unique short code
    [Required] public string OwnerName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public int PlanId { get; set; }
    public CpPlan? Plan { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMonths(1);

    public bool IsActive { get; set; } = true;

    // Usage (we'll update later automatically)
    public long CurrentDbBytes { get; set; } = 0;
}



