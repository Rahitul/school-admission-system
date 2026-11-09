using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Data;

namespace SchoolAdmission.Background;

public class ExpirySweeper : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpirySweeper> _logger;

    public ExpirySweeper(IServiceScopeFactory scopeFactory, ILogger<ExpirySweeper> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var affected = await db.Database.ExecuteSqlRawAsync(
                    "UPDATE holds SET \"Status\" = 'EXPIRED' WHERE \"Status\" = 'HELD' AND \"ExpiresAt\" <= {0}",
                    new object[] { now }, stoppingToken);
                if (affected > 0)
                    _logger.LogInformation("Expired {Count} holds", affected);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Expiry sweeper error");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}