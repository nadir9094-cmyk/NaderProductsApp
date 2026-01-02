using System;

namespace NaderProductsApp.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    // FIX: StartAt موجودة في DB كـ NOT NULL
    public DateTime StartAt { get; set; }

        public DateTime? ClosedAt { get; set; }
        public decimal OpeningCash { get; set; } = 0m;
        public decimal ClosingCash { get; set; } = 0m;
        public string? Note { get; set; }
        public bool IsClosed { get; set; } = false;
    }
}

