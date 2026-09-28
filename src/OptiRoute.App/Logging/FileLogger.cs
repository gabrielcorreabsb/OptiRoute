using System.IO;
using Microsoft.Extensions.Logging;

namespace OptiRoute.App.Logging;

/// <summary>
/// Provider de logging que escreve em arquivo texto, append-only, com lock compartilhado.
/// Mantém o app WPF diagnosticável sem depender de console (que não aparece quando
/// o .exe é lançado por clique-duplo). Caminho padrão: %APPDATA%\OptiRoute\OptiRoute.log.
/// <para>
/// NÃO depende de Serilog/NLog/etc. — implementação mínima suficiente para smoke tests.
/// </para>
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _lockObj = new();

    public FileLoggerProvider(string path) => _path = path;

    public ILogger CreateLogger(string categoryName) => new FileLogger(_path, _lockObj, categoryName);

    public void Dispose() { }

    private sealed class FileLogger : ILogger
    {
        private readonly string _path;
        private readonly object _lockObj;
        private readonly string _category;

        public FileLogger(string path, object lockObj, string category)
        {
            _path     = path;
            _lockObj  = lockObj;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel,-11}] {_category}: {formatter(state, exception)}";
            if (exception is not null)
                line += Environment.NewLine + exception;

            lock (_lockObj)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                    File.AppendAllText(_path, line + Environment.NewLine);
                }
                catch
                {
                    // Logging nunca pode quebrar o app. Se o disco está cheio ou
                    // o arquivo está bloqueado, engolimos a falha.
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
