using Microsoft.Extensions.Logging;
using AndroidLog = Android.Util.Log;

namespace KHost.Mobile.Diagnostics;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes to Android's logcat via <see cref="Android.Util.Log"/>.
/// </summary>
/// <remarks>
/// The stock <c>AddDebug()</c> provider routes to <see cref="System.Diagnostics.Debug"/>, whose output is
/// <em>discarded</em> when the app runs without a debugger attached (a plain <c>dotnet build -t:Run</c> deploy) —
/// so `ILogger` lines never reach logcat. This provider closes that gap: registered on the Android head in
/// <c>MauiProgram</c> (Debug only), every log lands under the fixed <c>KHostCue</c> tag, so
/// <c>adb logcat -s KHostCue</c> shows exactly the app's own diagnostics (HTTP, stores, the artwork flow).
/// Level filtering is still handled by the logging factory (see the AddFilter calls in <c>MauiProgram</c>).
/// </remarks>
public sealed class AndroidLogcatLoggerProvider : ILoggerProvider
{
    // Logcat tags are capped near 23 chars; keep a short, greppable app tag and carry the category in the message.
    private const string Tag = "KHostCue";

    public ILogger CreateLogger(string categoryName) => new AndroidLogcatLogger(categoryName);

    public void Dispose() { }

    private sealed class AndroidLogcatLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
                message = $"{message}\n{exception}";

            var line = $"{category}: {message}";
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    AndroidLog.Debug(Tag, line);
                    break;
                case LogLevel.Information:
                    AndroidLog.Info(Tag, line);
                    break;
                case LogLevel.Warning:
                    AndroidLog.Warn(Tag, line);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    AndroidLog.Error(Tag, line);
                    break;
                default:
                    AndroidLog.Info(Tag, line);
                    break;
            }
        }
    }
}
