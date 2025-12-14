using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NotiFIITBot.Consts; 

namespace NotiFIITBot.Database.Data;

public class ScheduleDbContextFactory : IDesignTimeDbContextFactory<ScheduleDbContext>
{
    public static string ConnectionString => 
        $"Host=localhost;" +
        $"Port=5434;" +
        $"Database={EnvReader.PostgresDbName};" + 
        $"Username={EnvReader.PostgresUser};" + 
        $"Password={EnvReader.PostgresPassword}";
    
    public ScheduleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
        optionsBuilder.UseNpgsql(ConnectionString);

        return new ScheduleDbContext(optionsBuilder.Options);
    }
}