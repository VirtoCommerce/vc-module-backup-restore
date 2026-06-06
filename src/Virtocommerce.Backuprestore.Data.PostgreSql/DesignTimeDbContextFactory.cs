using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Virtocommerce.Backuprestore.Data.Repositories;

namespace Virtocommerce.Backuprestore.Data.PostgreSql;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BackuprestoreDbContext>
{
    public BackuprestoreDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<BackuprestoreDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=localhost;Username=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(PostgreSqlDataAssemblyMarker).Assembly.GetName().Name));

        return new BackuprestoreDbContext(builder.Options);
    }
}
