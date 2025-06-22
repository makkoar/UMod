namespace SkyRez.Translator // Убедитесь, что пространство имен верное
{
    public static class PluginEntry
    {
        private static readonly Type ModType = typeof(TranslatorModule); // Убедитесь, что TranslatorModule существует в этом пространстве имен
        private static SkyRezMod modInstance; // Убрал '_' для соответствия вашему стилю

        // Переменные для отладочного лога
        private static string debugRootPathForLogs;
        private static string debugLogFilePath;

        static PluginEntry() // Статический конструктор для инициализации путей к логам один раз
        {
            try
            {
                // AppDomain.CurrentDomain.BaseDirectory здесь будет [GameName]_Data/
                string dataDir = AppDomain.CurrentDomain.BaseDirectory;
                debugRootPathForLogs = Directory.GetParent(dataDir)?.FullName ?? dataDir; // Если нет родителя, пишем в _Data
                debugLogFilePath = Path.Combine(debugRootPathForLogs, "PLUGIN_ENTRY_DEBUG.txt");

                // Очищаем лог при первой загрузке класса
                File.WriteAllText(debugLogFilePath, "--- PluginEntry Debug Log Session Started ---\r\n");
            }
            catch
            {
                // Если даже тут ошибка, ничего не поделать
            }
        }

        private static void WriteDebugLog(string message)
        {
            try
            {
                File.AppendAllText(debugLogFilePath, DateTime.Now.ToString("HH:mm:ss.fff") + ": " + message + "\r\n");
            }
            catch { /* Игнорируем ошибки записи в дебаг лог */ }
        }

        public static void Initialize()
        {
            WriteDebugLog("Initialize() - START");
            try
            {
                if (!SetupInfrastructure())
                {
                    WriteDebugLog("Initialize() - SetupInfrastructure FAILED. Exiting.");
                    return;
                }
                WriteDebugLog("Initialize() - SetupInfrastructure SUCCEEDED.");
                LoadMod();
                WriteDebugLog("Initialize() - LoadMod CALLED.");
            }
            catch (Exception ex)
            {
                WriteDebugLog("Initialize() - CRITICAL EXCEPTION: " + ex.ToString());
                // Попробуем записать в аварийный лог, если основной еще не работает
                try
                {
                    string emergencyLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EMERGENCY_LOG.txt");
                    File.AppendAllText(emergencyLogPath, DateTime.Now + ": КРИТИЧЕСКАЯ ОШИВКА в PluginEntry.Initialize: \r\n" + ex.ToString() + "\r\n");
                }
                catch { }
            }
            WriteDebugLog("Initialize() - END");
        }

        private static bool SetupInfrastructure()
        {
            WriteDebugLog("SetupInfrastructure() - START");
            try
            {
                // AppDomain.CurrentDomain.BaseDirectory здесь будет [GameName]_Data/
                string baseDataDirectory = AppDomain.CurrentDomain.BaseDirectory;
                WriteDebugLog("baseDataDirectory (AppDomain.CurrentDomain.BaseDirectory) = " + baseDataDirectory);

                // Получаем корневую папку игры, поднявшись на один уровень от папки _Data
                string gameRootPath = Directory.GetParent(baseDataDirectory)?.FullName;
                if (string.IsNullOrEmpty(gameRootPath))
                {
                    WriteDebugLog("SetupInfrastructure() - ERROR - Could not get gameRootPath from baseDataDirectory parent. baseDataDirectory was: " + baseDataDirectory);
                    return false;
                }
                WriteDebugLog("gameRootPath = " + gameRootPath);

                // Имя исполняемого файла игры (без .exe)
                string gameExePath = Directory.GetFiles(gameRootPath, "*.exe").FirstOrDefault();
                if (string.IsNullOrEmpty(gameExePath))
                {
                    WriteDebugLog("SetupInfrastructure() - ERROR - gameExePath is null or empty in gameRootPath: " + gameRootPath);
                    return false;
                }
                WriteDebugLog("gameExePath = " + gameExePath);

                string gameName = Path.GetFileNameWithoutExtension(gameExePath);
                WriteDebugLog("gameName (from exe) = " + gameName);

                // Формируем ожидаемое имя папки _Data (например, "DevilLegion_Data")
                string expectedUnityDataFolderName = gameName + "_Data";
                WriteDebugLog("expectedUnityDataFolderName (constructed from gameName) = " + expectedUnityDataFolderName);

                WriteDebugLog("Initializing PathResolver...");
                // Передаем в PathResolver имя игры и ОЖИДАЕМОЕ имя папки _Data
                // PathResolver будет использовать 'expectedUnityDataFolderName' при замене плейсхолдера {Data}
                PathResolver.Initialize(gameRootPath, gameName, expectedUnityDataFolderName);
                WriteDebugLog("PathResolver INITIALIZED. GameName: " + gameName + ", DataFolder (for {Data} placeholder): " + expectedUnityDataFolderName);

                // Формируем путь к файлу конфигурации, используя плейсхолдер {Data}
                // Патчер кладет SkyRez.Config.ini в папку [GameName]_Data/
                string configFileName = "SkyRez.Config.ini";
                string configPathPattern = Path.Combine("{Data}", configFileName); // "{Data}\SkyRez.Config.ini"
                WriteDebugLog("Config path pattern for Resolver = " + configPathPattern);

                string resolvedConfigPath = PathResolver.Resolve(configPathPattern);
                WriteDebugLog("Resolved configPath by PathResolver = " + resolvedConfigPath);
                // Ожидаемый путь: D:\User\Downloads\Attack.it.Devil.legion.v1.22\DevilLegion_Data\SkyRez.Config.ini

                WriteDebugLog("Loading ConfigManager with path: " + resolvedConfigPath);
                ConfigManager.Load(resolvedConfigPath);
                WriteDebugLog("ConfigManager LOADED. IsLoaded: " + ConfigManager.IsLoaded);

                WriteDebugLog("Initializing Logger...");
                Logger.Initialize(); // Logger прочитает настройки, включая LogPath, из ConfigManager
                WriteDebugLog("Logger INITIALIZED. LoggingEnabled from config: " + ConfigManager.GetBool("EnableLogging", false) +
                              ", Resolved LogPath by Logger: " + (Logger.IsInitialized ? " (Logger uses its internal LogPath)" : "N/A - Logger not fully init if config failed"));


                if (ConfigManager.IsLoaded && ConfigManager.GetBool("EnableLogging", true)) // По умолчанию EnableLogging = true
                {
                    Logger.Information("================================================");
                    Logger.Information("    Загрузчик модов SkyRez инициализирован (из PluginEntry)");
                    Logger.Information("    Версия Runtime: .NET " + Environment.Version.ToString());
                    Logger.Information("    Файл конфигурации успешно загружен. Путь: " + resolvedConfigPath);
                    Logger.Information("================================================");
                    WriteDebugLog("Main logger has written initial messages.");
                }
                else
                {
                    WriteDebugLog("Основной логгер НЕ будет писать, так как EnableLogging=false или файл конфигурации не найден/не загружен.");
                    if (!ConfigManager.IsLoaded)
                    {
                        WriteDebugLog("Причина: ConfigManager.IsLoaded is False. Файл конфигурации не найден по пути: " + resolvedConfigPath);
                    }
                    else if (!ConfigManager.GetBool("EnableLogging", true))
                    {
                        WriteDebugLog("Причина: EnableLogging в конфигурации установлен в false.");
                    }
                }
                WriteDebugLog("Main logger messages written (or skipped based on config).");
                WriteDebugLog("SetupInfrastructure() - SUCCEEDED");
                return true;
            }
            catch (Exception ex)
            {
                WriteDebugLog("SetupInfrastructure() - EXCEPTION: " + ex.ToString());
                return false;
            }
        }

        private static void LoadMod()
        {
            WriteDebugLog("LoadMod() - START");
            try
            {
                if (!typeof(SkyRezMod).IsAssignableFrom(ModType))
                {
                    Logger.Error("Тип '" + ModType.FullName + "' не является наследником SkyRezMod.");
                    WriteDebugLog("LoadMod() - ERROR - ModType not assignable from SkyRezMod: " + ModType.FullName);
                    return;
                }
                Logger.Information("Найден класс мода: " + ModType.FullName);
                WriteDebugLog("LoadMod() - Mod class found: " + ModType.FullName);

                modInstance = (SkyRezMod)Activator.CreateInstance(ModType);
                WriteDebugLog("LoadMod() - Mod instance created.");

                modInstance.OnLoad();
                WriteDebugLog("LoadMod() - modInstance.OnLoad() CALLED.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ошибка при загрузке или инициализации модуля мода.");
                WriteDebugLog("LoadMod() - EXCEPTION: " + ex.ToString());
            }
            WriteDebugLog("LoadMod() - END");
        }
    }
}