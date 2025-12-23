using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

[Flags]
public enum EmployeePermissions : long
{
    None = 0,

    Products = 1L << 0,            // المنتجات
    CustomersDeferred = 1L << 1,   // العملاء والدفع المؤجل
    Suppliers = 1L << 2,           // الموردين
    Expenses = 1L << 3,            // مصروفات المتجر
    Cashier = 1L << 4,             // شاشة الكاشير
    SalesInvoices = 1L << 5,       // فواتير المبيعات
    Employees = 1L << 6,           // الموظفين والصلاحيات
    Settings = 1L << 7,            // الاعدادات
    Backup = 1L << 8,              // النسخ الاحتياطي
    SubscriptionPlan = 1L << 9,    // خطة الاشتراك

    // مفيد: مدير النظام
    Admin = 1L << 60,
    All = ~0L
}

public class Employee
{
    public int Id { get; set; }

    [Required] public string FullName { get; set; } = "";
    [Required] public string Username { get; set; } = ""; // Unique

    // Password stored as PBKDF2 hash + salt (Base64)
    [Required] public string PasswordHash { get; set; } = "";
    [Required] public string PasswordSalt { get; set; } = "";

    public long Permissions { get; set; } = (long)EmployeePermissions.None;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
