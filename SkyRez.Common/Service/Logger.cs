namespace SkyRez.Common.Service;

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
            // Предотвращаем повторную инициализацию, если не нужно (например, если ConfigManager еще не загружен)
            // Но если ConfigManager уже был загружен, а isInitialized == true, это может быть повторный вызов от мода,
            // тогда мы сбрасываем isInitialized, чтобы пересчитать пути.
            if (isInitialized && !ConfigManager.IsLoaded) return; // Если ConfigManager не загружен, и уже инициализировались, выходим
            if (isInitialized && ConfigManager.IsLoaded && PathResolver.IsInitialized) isInitialized = false; // Позволяем переинициализацию модом

            if (isInitialized) return;

            consoleEnabled = GetConsoleWindow() != IntPtr.Zero;

            // Если ConfigManager еще не загружен (обычно при первом вызове из патчера)
            if (!ConfigManager.IsLoaded)
            {
                loggingEnabled = false; // По умолчанию для "сырого" старта патчера логирование в файл выключено
                                        // Патчер сам должен создать конфиг и потом переинициализировать логгер, если нужно
                resolvedLogPath = null;
            }
            else // ConfigManager загружен
            {
                loggingEnabled = ConfigManager.GetBool("EnableLogging", true); // В конфиге по умолчанию true

                if (loggingEnabled)
                {
                    // Патчер обычно использует путь без плейсхолдеров
                    string rawLogPathFromConfig = ConfigManager.GetString("LogPath", "./Logs/SkyRez.Patcher.Config.log");

                    try
                    {
                        if (PathResolver.IsInitialized) // Если PathResolver готов (например, это не патчер, а другой инструмент)
                            resolvedLogPath = PathResolver.Resolve(rawLogPathFromConfig);
                        else // PathResolver не готов (стандартная ситуация для патчера)
                            // Используем путь "как есть", но делаем его абсолютным относительно текущей директории патчера
                            resolvedLogPath = Path.IsPathRooted(rawLogPathFromConfig)
                                ? rawLogPathFromConfig
                                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rawLogPathFromConfig));

                        string logDirectory = Path.GetDirectoryName(resolvedLogPath);
                        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                            Directory.CreateDirectory(logDirectory);
                        // При инициализации создаем/перезаписываем файл лога
                        File.WriteAllText(resolvedLogPath, $"--- Сессия логирования начата в {DateTime.Now} ---\r\n");
                    }
                    catch (Exception ex)
                    {
                        loggingEnabled = false;
                        resolvedLogPath = null;
                        if (consoleEnabled)
                        {
                            ConsoleColor originalColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[ОШИБКА ИНИЦИАЛИЗАЦИИ ЛОГГЕРА] Не удалось настроить файловый лог. Путь: '{rawLogPathFromConfig}'. Ошибка: {ex.Message}. Логирование в файл отключено.");
                            Console.ResetColor();
                        }
                    }
                }
                else
                    resolvedLogPath = null; // Логирование в файл отключено в конфиге
            }
            isInitialized = true;
        }
    }

    public static void Debug(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Debug, source, message);

    public static void Verbose(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Verbose, source, message);

    public static void Information(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Information, source, message);

    public static void Warning(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, message);

    public static void Warning(Exception ex, string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, new StringBuilder()
            .AppendLine(message)
            .AppendLine("------------ ИСКЛЮЧЕНИЕ ------------")
            .AppendLine(ex.ToString())
            .Append("------------------------------------").ToString());

    public static void Error(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, message);

    public static void Error(Exception ex, string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, new StringBuilder()
            .AppendLine(message)
            .AppendLine("------------ ИСКЛЮЧЕНИЕ ------------")
            .AppendLine(ex.ToString())
            .Append("------------------------------------").ToString());

    private static void Log(ELogLevel level, string source, string message)
    {
        if (!isInitialized) return; // Если даже базовая инициализация не прошла, не логируем
        if (!loggingEnabled && !consoleEnabled) return; // Если оба типа логирования выключены

        lock (_lock)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff");
            string state = $"[{level.ToString().ToUpper()}]";
            string logEntry = $"[{timestamp}] {state} [{source}]: {message}\r\n";

            if (loggingEnabled && !string.IsNullOrEmpty(resolvedLogPath))
                try { File.AppendAllText(resolvedLogPath, logEntry); }
                catch { /* Тихо игнорируем ошибки записи */ }

            if (consoleEnabled)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = level switch
                {
                    ELogLevel.Error => ConsoleColor.Red,
                    ELogLevel.Warning => ConsoleColor.Yellow,
                    ELogLevel.Information => ConsoleColor.White,
                    ELogLevel.Debug => ConsoleColor.Gray,
                    ELogLevel.Verbose => ConsoleColor.DarkGray,
                    _ => ConsoleColor.White
                };
                Console.Write(logEntry);
                Console.ForegroundColor = originalColor;
            }
        }
    }
}