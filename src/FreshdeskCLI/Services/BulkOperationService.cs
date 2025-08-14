using FreshdeskCLI.Helpers;
using FreshdeskCLI.Models;

namespace FreshdeskCLI.Services;

public interface IBulkOperationService
{
    Task<BulkOperationResult<T>> ProcessBulkAsync<T>(
        IEnumerable<long> ids,
        Func<long, Task<T>> operation,
        string operationName,
        int maxConcurrency = 5,
        bool showProgress = true,
        CancellationToken cancellationToken = default);

    Task<BulkDownloadResult> DownloadAttachmentsAsync(
        Ticket ticket,
        string downloadPath,
        bool showProgress = true,
        CancellationToken cancellationToken = default);
}

public sealed class BulkOperationService : IBulkOperationService
{
    private readonly IFreshdeskApiClient _apiClient;

    public BulkOperationService(IFreshdeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<BulkOperationResult<T>> ProcessBulkAsync<T>(
        IEnumerable<long> ids,
        Func<long, Task<T>> operation,
        string operationName,
        int maxConcurrency = 5,
        bool showProgress = true,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        var results = new List<T>();
        var errors = new List<BulkOperationError>();

        using var progress = ProgressIndicatorFactory.Create(
            $"Processing {operationName}",
            enabled: showProgress);

        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var completed = 0;
        var total = idList.Count;

        var tasks = idList.Select(async id =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await operation(id);
                lock (results)
                {
                    results.Add(result);
                    completed++;
                    progress.Report(completed, total, $"Processed {completed}/{total}");
                }
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors.Add(new BulkOperationError
                    {
                        Id = id,
                        Error = ex.Message
                    });
                    completed++;
                    progress.Report(completed, total, $"Processed {completed}/{total} (with errors)");
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        progress.Complete($"Completed {results.Count} successfully, {errors.Count} errors");

        return new BulkOperationResult<T>
        {
            Successful = results,
            Errors = errors,
            TotalProcessed = idList.Count,
            SuccessCount = results.Count,
            ErrorCount = errors.Count
        };
    }

    public async Task<BulkDownloadResult> DownloadAttachmentsAsync(
        Ticket ticket,
        string downloadPath,
        bool showProgress = true,
        CancellationToken cancellationToken = default)
    {
        if (ticket.Attachments == null || ticket.Attachments.Length == 0)
        {
            return new BulkDownloadResult
            {
                TotalFiles = 0,
                SuccessCount = 0,
                ErrorCount = 0,
                TotalBytes = 0,
                Files = Array.Empty<string>(),
                Errors = Array.Empty<BulkOperationError>()
            };
        }

        Directory.CreateDirectory(downloadPath);

        var files = new List<string>();
        var errors = new List<BulkOperationError>();
        var totalBytes = 0L;

        using var progress = ProgressIndicatorFactory.Create(
            $"Downloading attachments for ticket #{ticket.Id}",
            enabled: showProgress);

        var completed = 0;
        var total = ticket.Attachments.Length;

        foreach (var attachment in ticket.Attachments)
        {
            try
            {
                progress.Report(completed, total, $"Downloading {attachment.Name}");

                if (string.IsNullOrEmpty(attachment.AttachmentUrl))
                {
                    throw new InvalidOperationException($"No URL for attachment {attachment.Name}");
                }

                var content = await _apiClient.DownloadAttachmentAsync(attachment.AttachmentUrl, cancellationToken);

                var safeFileName = GetSafeFileName(attachment.Name ?? $"attachment_{attachment.Id}");
                var filePath = Path.Combine(downloadPath, safeFileName);

                // Handle duplicate file names
                var counter = 1;
                while (File.Exists(filePath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);
                    var ext = Path.GetExtension(safeFileName);
                    filePath = Path.Combine(downloadPath, $"{nameWithoutExt}_{counter}{ext}");
                    counter++;
                }

                await File.WriteAllBytesAsync(filePath, content, cancellationToken);

                files.Add(filePath);
                totalBytes += content.Length;
                completed++;

                progress.Report(completed, total, $"Downloaded {attachment.Name} ({FormatBytes(content.Length)})");
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationError
                {
                    Id = attachment.Id,
                    Error = $"Failed to download {attachment.Name}: {ex.Message}"
                });
                completed++;
                progress.Report(completed, total, $"Failed: {attachment.Name}");
            }
        }

        progress.Complete($"Downloaded {files.Count}/{total} files ({FormatBytes(totalBytes)})");

        return new BulkDownloadResult
        {
            TotalFiles = total,
            SuccessCount = files.Count,
            ErrorCount = errors.Count,
            TotalBytes = totalBytes,
            Files = files.ToArray(),
            Errors = errors.ToArray()
        };
    }

    private static string GetSafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = fileName;
        foreach (var c in invalid)
        {
            safe = safe.Replace(c, '_');
        }
        return safe;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public sealed class BulkOperationResult<T>
{
    public required List<T> Successful { get; init; }
    public required List<BulkOperationError> Errors { get; init; }
    public required int TotalProcessed { get; init; }
    public required int SuccessCount { get; init; }
    public required int ErrorCount { get; init; }
}

public sealed class BulkDownloadResult
{
    public required int TotalFiles { get; init; }
    public required int SuccessCount { get; init; }
    public required int ErrorCount { get; init; }
    public required long TotalBytes { get; init; }
    public required string[] Files { get; init; }
    public required BulkOperationError[] Errors { get; init; }
}

public sealed class BulkOperationError
{
    public required long Id { get; init; }
    public required string Error { get; init; }
}