using System.Reflection;
using Microsoft.EntityFrameworkCore;
//using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace Virtocommerce.Backuprestore.Data.Repositories;

public class BackuprestoreDbContext : DbContextBase
{
    public BackuprestoreDbContext(DbContextOptions<BackuprestoreDbContext> options)
        : base(options)
    {
    }

    protected BackuprestoreDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //modelBuilder.Entity<BazQuxEntity>().ToAuditableEntityTable("BazQux");

        switch (Database.ProviderName)
        {
            case "Pomelo.EntityFrameworkCore.MySql":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("Virtocommerce.Backuprestore.Data.MySql"));
                break;
            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("Virtocommerce.Backuprestore.Data.PostgreSql"));
                break;
            case "Microsoft.EntityFrameworkCore.SqlServer":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("Virtocommerce.Backuprestore.Data.SqlServer"));
                break;
        }
    }
}
