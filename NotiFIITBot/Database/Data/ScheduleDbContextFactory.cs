using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NotiFIITBot.Consts; 

namespace NotiFIITBot.Database.Data;

public class ScheduleDbContextFactory : IDesignTimeDbContextFactory<ScheduleDbContext>
{
    public ScheduleDbContext CreateDbContext(string[] args)
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var envPath = Path.Combine(projectRoot, ".env");
        EnvReader.Load(envPath);

        var connectionString =
            $"Host=localhost;" +
            $"Port=5434;" +
            $"Database={EnvReader.PostgresDbName};" + 
            $"Username={EnvReader.PostgresUser};" + 
            $"Password={EnvReader.PostgresPassword}";

        var optionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ScheduleDbContext(optionsBuilder.Options);
    }
}