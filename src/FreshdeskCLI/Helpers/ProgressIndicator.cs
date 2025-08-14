using System.Diagnostics;

namespace FreshdeskCLI.Helpers;

public interface IProgressIndicator : IDisposable
{
    void Report(int current, int total, string? message = null);
    void Report(double percentage, string? message = null);
    void Complete(string? message = null);
}

public sealed class ConsoleProgressIndicator : IProgressIndicator
{
    private readonly string _title;
    private readonly bool _showPercentage;
    private readonly bool _showSpinner;
    private readonly Stopwatch _stopwatch;
    private readonly object _lock = new();
    private int _lastLineLength;
    private int _spinnerIndex;
    private bool _isCompleted;

    private static readonly string[] SpinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    public ConsoleProgressIndicator(string title, bool showPercentage = true, bool showSpinner = true)
    {
        _title = title;
        _showPercentage = showPercentage;
        _showSpinner = showSpinner;
        _stopwatch = Stopwatch.StartNew();

        // Initial display
        Console.Write($"{_title}... ");
        if (_showSpinner)
        {
            Console.Write(SpinnerFrames[0]);
        }
    }

    public void Report(int current, int total, string? message = null)
    {
        if (total <= 0) return;

        var percentage = (double)current / total;
        Report(percentage, message);
    }

    public void Report(double percentage, string? message = null)
    {
        lock (_lock)
        {
            if (_isCompleted) return;

            // Clear the current line
            Console.Write($"\r{new string(' ', _lastLineLength)}\r");

            var output = _title;

            if (_showSpinner)
            {
                _spinnerIndex = (_spinnerIndex + 1) % SpinnerFrames.Length;
                output += $" {SpinnerFrames[_spinnerIndex]}";
            }

            if (_showPercentage)
            {
                var percent = Math.Min(100, Math.Max(0, percentage * 100));
                output += $" [{percent:0}%]";

                // Add progress bar
                var barWidth = 20;
                var completed = (int)(barWidth * percentage);
                var bar = new string('█', completed) + new string('░', barWidth - completed);
                output += $" {bar}";
            }

            if (!string.IsNullOrEmpty(message))
            {
                output += $" - {message}";
            }

            // Add elapsed time
            var elapsed = _stopwatch.Elapsed;
            if (elapsed.TotalSeconds > 2)
            {
                output += $" ({FormatTime(elapsed)})";
            }

            Console.Write(output);
            _lastLineLength = output.Length;
        }
    }

    public void Complete(string? message = null)
    {
        lock (_lock)
        {
            if (_isCompleted) return;
            _isCompleted = true;

            _stopwatch.Stop();

            // Clear the current line
            Console.Write($"\r{new string(' ', _lastLineLength)}\r");

            var output = $"✓ {_title}";
            if (!string.IsNullOrEmpty(message))
            {
                output += $" - {message}";
            }
            output += $" ({FormatTime(_stopwatch.Elapsed)})";

            Console.WriteLine(output);
        }
    }

    public void Dispose()
    {
        if (!_isCompleted)
        {
            Complete();
        }
    }

    private static string FormatTime(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalSeconds >= 1)
            return $"{timeSpan.Seconds}.{timeSpan.Milliseconds / 100}s";
        return $"{timeSpan.Milliseconds}ms";
    }
}

public sealed class SilentProgressIndicator : IProgressIndicator
{
    public void Report(int current, int total, string? message = null) { }
    public void Report(double percentage, string? message = null) { }
    public void Complete(string? message = null) { }
    public void Dispose() { }
}

public static class ProgressIndicatorFactory
{
    public static IProgressIndicator Create(string title, bool enabled = true, bool showPercentage = true, bool showSpinner = true)
    {
        if (!enabled || Console.IsOutputRedirected)
        {
            return new SilentProgressIndicator();
        }

        return new ConsoleProgressIndicator(title, showPercentage, showSpinner);
    }
}