using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Zahy.Finance;

public sealed class RecordingLoggerProvider : ILoggerProvider
{
    public List<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages, categoryName);

    public void Dispose()
    {
    }

    public sealed class RecordingLogger : ILogger
    {
        private readonly List<string> _messages;
        private readonly string _categoryName;

        public RecordingLogger(List<string> messages, string categoryName)
        {
            _messages = messages;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Add($"[{_categoryName}] {formatter(state, exception)}");
        }
    }
}
