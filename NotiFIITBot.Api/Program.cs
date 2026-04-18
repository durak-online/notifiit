using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Consts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:63343") // Разрешаем конкретно ваш фронтенд
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