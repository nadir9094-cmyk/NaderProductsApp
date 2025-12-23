using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models.ControlPanel;

public class CpAdminSession
{
    public int Id { get; set; }

    public int AdminUserId { get; set; }
    public CpAdminUser? AdminUser { get; set; }

    [Required] public string Token { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? RevokedAt { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
