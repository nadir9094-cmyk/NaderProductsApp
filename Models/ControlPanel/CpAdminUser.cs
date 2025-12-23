using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models.ControlPanel;

public class CpAdminUser
{
    public int Id { get; set; }

    [Required] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";

    public string FullName { get; set; } = "Super Admin";
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
