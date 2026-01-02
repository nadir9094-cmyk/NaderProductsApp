using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

public static class BackupFeature
{
    // Safe no-op: يمنع أخطاء Docker build إذا BackupSettings/DailyBackupService غير موجودة
    public static IServiceCollection AddNaderBackup(this IServiceCollection services, IConfiguration cfg)
    {
        // intentionally disabled
        return services;
    }
}
