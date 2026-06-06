using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Virtocommerce.Backuprestore.Data.Repositories;

namespace Virtocommerce.Backuprestore.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BackuprestoreDbContext>
{
    public BackuprestoreDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<BackuprestoreDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new BackuprestoreDbContext(builder.Options);
    }
}
