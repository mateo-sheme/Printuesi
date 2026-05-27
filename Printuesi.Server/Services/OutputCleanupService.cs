// Services/OutputCleanupService.cs
namespace Printuesi.Server.Services
{
    public class OutputCleanupService : BackgroundService
    {
        private readonly ILogger<OutputCleanupService> _logger;
        private readonly string _outputBasePath;
        private readonly TimeSpan _retention = TimeSpan.FromDays(28);
        private readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public OutputCleanupService(ILogger<OutputCleanupService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _outputBasePath = Path.Combine(env.WebRootPath, "labels_output", "jobs");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                CleanOldFiles();
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private void CleanOldFiles()
        {
            if (!Directory.Exists(_outputBasePath))
                return;

            var cutoff = DateTime.UtcNow - _retention;
            var deleted = 0;

            foreach (var jobDir in Directory.GetDirectories(_outputBasePath))
            {
                try
                {
                    var created = Directory.GetCreationTimeUtc(jobDir);
                    if (created < cutoff)
                    {
                        Directory.Delete(jobDir, recursive: true);
                        deleted++;
                        _logger.LogInformation("Deleted old job output: {Dir}", jobDir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to delete {Dir}: {Message}", jobDir, ex.Message);
                }
            }

            if (deleted > 0)
                _logger.LogInformation("Cleanup complete — {Count} old job folders deleted", deleted);
        }
    }
}