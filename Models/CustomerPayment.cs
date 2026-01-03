using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class CustomerPayment
{
    public int Id { get; set; }
    [Required] public int CustomerId { get; set; }

    public double Amount { get; set; } = 0;
    public string Method { get; set; } = "كاش";
    public string? Note { get; set; }
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

    public Customer? Customer { get; set; }
}

