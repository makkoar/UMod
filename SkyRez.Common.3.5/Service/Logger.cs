// Файл: SkyRez.Common.3.5/Logger.cs
using SkyRez.Common.Resolvers;

public static class Logger
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    private static bool isInitialized = false;
    public static bool IsInitialized { get { return isInitialized; } }

    private static readonly object _lock = new object();
    private static bool loggingEnabled = false;
    private static bool consoleEnabled = false;
    private static string resolvedLogPath; // Финальный, разрешенный путь к файлу лога

    public static bool IsLoggingToFileEnabled { get { return loggingEnabled && !string.IsNullOrEmpty(resolvedLogPath); } }

    public static void Initialize()
    {
        lock (_lock)
        {
            // Если Logger.Initialize() вызывается модом, ConfigManager и PathResolver уже должны быть готовы.
            // Повторный вызов сбросит isInitialized и пересчитает пути.
            if (isInitialized && ConfigManager.IsLoaded && PathResolver.IsInitialized)
            {
                isInitialized = false;
            }
            if (isInitialized) return;

            consoleEnabled = GetConsoleWindow() != IntPtr.Zero;

            // Мод ВСЕГДА должен вызывать Logger.Initialize ПОСЛЕ ConfigManager.Load и PathResolver.Initialize
            if (!ConfigManager.IsLoaded || !PathResolver.IsInitialized)
            {
                loggingEnabled = false; // Не можем корректно настроить логирование
                isInitialized = true; // Базовая инициализация для консоли
                if (consoleEnabled)
                {
                    ConsoleColor originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Magenta; // Отличительный цвет для этой ошибки
                    Console.WriteLine("[КРИТИЧЕСКАЯ ОШИБКА ЛОГГЕРА] Logger.Initialize вызван до ConfigManager или PathResolver! Логирование в файл не будет работать.");
                    Console.ResetColor();
                }
                return;
            }

            loggingEnabled = ConfigManager.GetBool("EnableLogging", true); // В конфиге по умолчанию true

            if (loggingEnabled)
            {
                // Мод ожидает путь с плейсхолдерами
                string rawLogPathFromConfig = ConfigManager.GetString("LogPath", @"Logs\SkyRez.{Name}.log");

                try
                {
                    // PathResolver должен быть инициализирован!
                    resolvedLogPath = PathResolver.Resolve(rawLogPathFromConfig);

                    string logDirectory = Path.GetDirectoryName(resolvedLogPath);
                    if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }
                    File.WriteAllText(resolvedLogPath, "--- Сессия логирования начата в " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " ---\r\n");
                }
                catch (InvalidOperationException ex) // PathResolver не был инициализирован, хотя должен был
                {
                    loggingEnabled = false; resolvedLogPath = null;
                    if (consoleEnabled)
                    {
                        ConsoleColor originalColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[ОШИБКА ИНИЦИАЛИЗАЦИИ ЛОГГЕРА] PathResolver не был инициализирован до вызова Logger.Initialize! Ошибка: " + ex.Message + ". Логирование в файл отключено.");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex) // Другие ошибки
                {
                    loggingEnabled = false; resolvedLogPath = null;
                    if (consoleEnabled)
                    {
                        ConsoleColor originalColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[ОШИБКА ИНИЦИАЛИЗАЦИИ ЛОГГЕРА] Не удалось настроить файловый лог. Путь: '" + rawLogPathFromConfig + "'. Ошибка: " + ex.Message + ". Логирование в файл отключено.");
                        Console.ResetColor();
                    }
                }
            }
            else
            {
                resolvedLogPath = null; // Логирование в файл отключено в конфиге
            }
            isInitialized = true;
        }
    }

    public static void Debug(string message) =>
        Log(ELogLevel.Debug, new StackFrame(1, false).GetMethod().Name, message);

    public static void Verbose(string message) =>
        Log(ELogLevel.Verbose, new StackFrame(1, false).GetMethod().Name, message);

    public static void Information(string message) =>
        Log(ELogLevel.Information, new StackFrame(1, false).GetMethod().Name, message);

    public static void Warning(string message) =>
        Log(ELogLevel.Warning, new StackFrame(1, false).GetMethod().Name, message);

    public static void Warning(Exception ex, string message)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine("------------ ИСКЛЮЧЕНИЕ ------------");
        sb.AppendLine(ex.ToString());
        sb.Append("------------------------------------");
        Log(ELogLevel.Warning, new StackFrame(1, false).GetMethod().Name, sb.ToString());
    }

    public static void Error(string message) =>
        Log(ELogLevel.Error, new StackFrame(1, false).GetMethod().Name, message);

    public static void Error(Exception ex, string message)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine("------------ ИСКЛЮЧЕНИЕ ------------");
        sb.AppendLine(ex.ToString());
        sb.Append("------------------------------------");
        Log(ELogLevel.Error, new StackFrame(1, false).GetMethod().Name, sb.ToString());
    }

    private static void Log(ELogLevel level, string source, string message)
    {
        if (!isInitialized) return;
        if (!loggingEnabled && !consoleEnabled) return;

        lock (_lock)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff");
            string state = "[" + level.ToString().ToUpper() + "]";
            string logEntry = "[" + timestamp + "] " + state + " [" + source + "]: " + message + "\r\n";

            if (loggingEnabled && !string.IsNullOrEmpty(resolvedLogPath))
            {
                try { File.AppendAllText(resolvedLogPath, logEntry); }
                catch { }
            }

            if (consoleEnabled)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                ConsoleColor newColor = ConsoleColor.White;
                switch (level)
                {
                    case ELogLevel.Error: newColor = ConsoleColor.Red; break;
                    case ELogLevel.Warning: newColor = ConsoleColor.Yellow; break;
                    // Information остается White по умолчанию
                    case ELogLevel.Debug: newColor = ConsoleColor.Gray; break;
                    case ELogLevel.Verbose: newColor = ConsoleColor.DarkGray; break;
                }
                Console.ForegroundColor = newColor;
                Console.Write(logEntry);
                Console.ForegroundColor = originalColor;
            }
        }
    }
}