using System.Collections.Concurrent;
using System.Text;

namespace API.HR.Node.Services.Logging;

public class FileLoggerOptions
{
    public string LogDirectory { get; set; } = "logs";
    public string FilePrefix { get; set; } = "app-log-";
    public int RetainDays { get; set; } = 30;
}

[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly BlockingCollection<string> _entryQueue = new(10000);
    private readonly Task _processTask;
    private readonly CancellationTokenSource _cts = new();

    public FileLoggerProvider(FileLoggerOptions options)
    {
        _options = options;

        if (!Directory.Exists(_options.LogDirectory))
        {
            Directory.CreateDirectory(_options.LogDirectory);
        }

        _processTask = Task.Run(ProcessQueueAsync);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    internal void WriteLog(string formattedEntry)
    {
        if (!_cts.IsCancellationRequested)
        {
            _entryQueue.TryAdd(formattedEntry);
        }
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new StringBuilder();
        string? lastPruneDate = null;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_entryQueue.TryTake(out var entry, 100, _cts.Token))
                {
                    // Drain whatever else is queued and write it in one append,
                    // instead of opening and closing the file once per line.
                    batch.Clear().Append(entry).Append(Environment.NewLine);
                    while (batch.Length < 256 * 1024 && _entryQueue.TryTake(out var next))
                    {
                        batch.Append(next).Append(Environment.NewLine);
                    }

                    var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    var fileName = $"{_options.FilePrefix}{dateStr}.log";
                    var filePath = Path.Combine(_options.LogDirectory, fileName);

                    await File.AppendAllTextAsync(filePath, batch.ToString(), Encoding.UTF8);

                    if (lastPruneDate != dateStr)
                    {
                        lastPruneDate = dateStr;
                        PruneOldFiles();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore background logging errors to keep the pipeline stable
            }
        }
    }

    /// <summary>Deletes log files older than RetainDays; runs once per day, on the first write.</summary>
    private void PruneOldFiles()
    {
        if (_options.RetainDays <= 0) return;

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetainDays);
        foreach (var file in Directory.EnumerateFiles(_options.LogDirectory, _options.FilePrefix + "*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
            }
            catch
            {
                // A locked or vanished file is retried tomorrow.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _processTask.Wait(1000);
        }
        catch { }
        _cts.Dispose();
        _entryQueue.Dispose();
        _loggers.Clear();
    }
}

public sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerProvider _provider;

    public FileLogger(string categoryName, FileLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null) return;

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var sb = new StringBuilder();
        sb.Append($"[{timestamp}] [{logLevel}] [{_categoryName}] {message}");

        if (exception != null)
        {
            sb.AppendLine();
            sb.Append($"  Exception: {exception.GetType().FullName}: {exception.Message}");
            sb.AppendLine();
            sb.Append($"  StackTrace: {exception.StackTrace}");
        }

        _provider.WriteLog(sb.ToString());
    }
}

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<FileLoggerOptions>? configure = null)
    {
        var options = new FileLoggerOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton<ILoggerProvider>(new FileLoggerProvider(options));
        return builder;
    }
}
