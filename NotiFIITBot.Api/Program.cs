using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Consts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins("https://kin211.github.io/notifiit_web")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
    options.AddPolicy("AllowDynamicOrigins", policy =>
    {
        policy.SetIsOriginAllowed(origin => origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var connectionString = $"Host=localhost;" +
                       $"Port=5434;Database={EnvReader.PostgresDbName};" +
                       $"Username={EnvReader.PostgresUser};" +
                       $"Password={EnvReader.PostgresPassword}";
builder.Services.AddDbContext<ScheduleDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

var app = builder.Build();
app.UseCors("AllowAll");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();