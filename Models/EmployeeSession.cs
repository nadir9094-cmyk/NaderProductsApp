namespace NaderProductsApp.Models;

public class EmployeeSession
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string Token { get; set; } = ""; // random GUID
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
