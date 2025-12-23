using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models.ControlPanel;

public class CpPlan
{
    public int Id { get; set; }

    [Required] public string Code { get; set; } = "basic";   // basic/pro/enterprise
    [Required] public string Name { get; set; } = "Basic";

    // SAR
    public decimal PriceMonthly { get; set; } = 0m;

    // Limits
    public int MaxStores { get; set; } = 1;
    public long MaxDbBytes { get; set; } = 1024L * 1024 * 1024; // 1GB default

    public bool IsActive { get; set; } = true;
}
