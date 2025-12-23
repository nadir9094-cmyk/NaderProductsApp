using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class Customer
{
    public int Id { get; set; }

    [Required] public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public string Status { get; set; } = "active";

    public List<CustomerInvoice> Invoices { get; set; } = new();
    public List<CustomerPayment> Payments { get; set; } = new();
}
