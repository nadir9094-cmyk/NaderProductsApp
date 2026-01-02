// BackupFeature.cs (clean)
// Replace your existing file at: C:\sami\BackupFeature.cs
// NOTE: BackupSettings / BackupRunner / DailyBackupService must exist ONLY ONCE (in Program.Types.cs).

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

public static class BackupFeature
{
    // Optional helper. If you don't call it, it's still safe to keep this file.
    public static IServiceCollection AddNaderBackup(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<BackupSettings>(cfg.GetSection("BackupSettings"));
        services.AddHostedService<DailyBackupService>();
        return services;
    }
}
