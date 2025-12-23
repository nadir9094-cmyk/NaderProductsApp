namespace NaderProductsApp.Models;

public class Expense
{
    public int Id { get; set; }

    public string? Title { get; set; }          // اسم المصروف
    public string? Category { get; set; }       // تصنيف
    public decimal Amount { get; set; }         // المبلغ
    public string? Note { get; set; }           // ملاحظة
    public string? PaymentMethod { get; set; }  // كاش/شبكة...

    public string? Statement { get; set; }      // بيان/وصف مختصر

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    // Alias للتوافق مع أي كود قديم يستخدم Expense.Date
    public DateTime Date
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }
}
