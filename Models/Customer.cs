using System;
using System.Collections.Generic;

namespace NaderProductsApp.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }

        // active, suspended_temp, suspended_perm
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CustomerInvoice> Invoices { get; set; } = new List<CustomerInvoice>();
        public ICollection<CustomerPayment> Payments { get; set; } = new List<CustomerPayment>();
    }

    public class CustomerInvoice
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }

    public class CustomerPayment
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public decimal Amount { get; set; }

        // "كاش" أو "شبكة"
        public string Method { get; set; } = "كاش";

        public string? Note { get; set; }
    }
}
