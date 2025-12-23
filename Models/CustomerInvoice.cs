using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class CustomerInvoice
{
    public int Id { get; set; }
    [Required] public int CustomerId { get; set; }

    public double Amount { get; set; } = 0;
    public string Description { get; set; } = "تعديل رصيد يدوي";
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

    public Customer? Customer { get; set; }
}
