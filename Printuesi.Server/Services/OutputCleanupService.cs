// Services/OutputCleanupService.cs
using System.Globalization;

namespace Printuesi.Server.Services
{
    public class OutputCleanupService : BackgroundService
    {
        private readonly ILogger<OutputCleanupService> _logger;
        private readonly IConfiguration _config;

        private readonly string _labelsOutputRoot;
        private readonly string _jobsOutputPath;
        private readonly string _manualOutputPath;

        public OutputCleanupService(
            ILogger<OutputCleanupService> logger,
            IWebHostEnvironment env,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;

            var webRoot = env.WebRootPath
                ?? Path.Combine(env.ContentRootPath, "wwwroot");

            _labelsOutputRoot = Path.Combine(webRoot, "labels_output");
            _jobsOutputPath = Path.Combine(_labelsOutputRoot, "jobs");
            _manualOutputPath = Path.Combine(_labelsOutputRoot, "manual");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var runEveryHours = _config.GetValue<int?>("OutputCleanup:RunEveryHours") ?? 24;

            _logger.LogInformation(
                "OutputCleanupService started. Runs every {Hours} hours.",
                runEveryHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CleanOldOutputs();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Output cleanup failed.");
                }

                await Task.Delay(TimeSpan.FromHours(runEveryHours), stoppingToken);
            }
        }

        private void CleanOldOutputs()
        {
            var retentionDays = _config.GetValue<int?>("OutputCleanup:RetentionDays") ?? 30;

            var cutoffDate = DateTime.Today.AddDays(-retentionDays);

            _logger.LogInformation(
                "Output cleanup started. Retention: {Days} days. Cutoff: {Cutoff:yyyy-MM-dd}",
                retentionDays,
                cutoffDate);

            CleanOldJobFolders(cutoffDate);
            CleanOldManualDailyFolders(cutoffDate);
            CleanOldLooseManualPdfFiles(cutoffDate);
        }

        private void CleanOldJobFolders(DateTime cutoffDate)
        {
            if (!Directory.Exists(_jobsOutputPath))
                return;

            var deleted = 0;

            foreach (var jobDir in Directory.GetDirectories(_jobsOutputPath))
            {
                try
                {
                    var lastWriteDate = Directory.GetLastWriteTime(jobDir).Date;
                    var createdDate = Directory.GetCreationTime(jobDir).Date;
                    var folderDate = lastWriteDate > createdDate ? lastWriteDate : createdDate;

                    if (folderDate >= cutoffDate)
                        continue;

                    Directory.Delete(jobDir, recursive: true);
                    deleted++;

                    _logger.LogInformation("Deleted old job output folder: {Dir}", jobDir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old job folder: {Dir}", jobDir);
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Job cleanup complete. Deleted {Count} old job folders.",
                    deleted);
            }
        }

        private void CleanOldManualDailyFolders(DateTime cutoffDate)
        {
            if (!Directory.Exists(_manualOutputPath))
                return;

            var deleted = 0;

            foreach (var dayDir in Directory.GetDirectories(_manualOutputPath))
            {
                var folderName = Path.GetFileName(dayDir);

                if (!DateTime.TryParseExact(
                        folderName,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var folderDate))
                {
                    // Skip folders that are not daily folders.
                    continue;
                }

                if (folderDate >= cutoffDate)
                    continue;

                try
                {
                    Directory.Delete(dayDir, recursive: true);
                    deleted++;

                    _logger.LogInformation("Deleted old manual output folder: {Dir}", dayDir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old manual folder: {Dir}", dayDir);
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Manual cleanup complete. Deleted {Count} old daily folders.",
                    deleted);
            }
        }

        private void CleanOldLooseManualPdfFiles(DateTime cutoffDate)
        {
            if (!Directory.Exists(_manualOutputPath))
                return;

            var deleted = 0;

            foreach (var file in Directory.GetFiles(_manualOutputPath, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var fileDate = File.GetLastWriteTime(file).Date;

                    if (fileDate >= cutoffDate)
                        continue;

                    File.Delete(file);
                    deleted++;

                    _logger.LogInformation("Deleted old loose manual PDF: {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old loose manual PDF: {File}", file);
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Loose manual PDF cleanup complete. Deleted {Count} files.",
                    deleted);
            }
        }
    }
}