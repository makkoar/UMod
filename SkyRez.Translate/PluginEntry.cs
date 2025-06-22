namespace SkyRez.Translate;

/// <summary>Статический класс, отвечающий за инициализацию и загрузку мода.</summary>
/// <remarks>Вызывается из <see cref="ComBridge"/>.</remarks>
public static class PluginEntry
{
    #region Приватные поля

    /// <summary>Тип основного класса мода, который будет инстанцирован и загружен.</summary>
    /// <value>Должен быть тип, унаследованный от <see cref="SkyRezMod"/>.</value>
    private static readonly Type modType = typeof(TranslatorModule);

    /// <summary>Экземпляр загруженного мода.</summary>
    private static SkyRezMod modInstance;

    #endregion

    #region Инициализация

    /// <summary>Основной метод инициализации мода.</summary>
    /// <remarks>Настраивает инфраструктуру (PathResolver, ConfigManager, Logger) и загружает мод.</remarks>
    public static void Initialize()
    {
        try
        {
            if (!SetupInfrastructure()) return;
            LoadMod();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "КРИТИЧЕСКАЯ ОШИБКА верхнего уровня в PluginEntry.Initialize()");
        }
    }

    /// <summary>Настраивает основные компоненты инфраструктуры мода: <see cref="PathResolver"/>, <see cref="ConfigManager"/> и <see cref="Logger"/>.</summary>
    /// <returns><c>true</c> если инициализация прошла успешно, иначе <c>false</c>.</returns>
    private static bool SetupInfrastructure()
    {
        try
        {
            string baseDataDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string gameRootPath = Directory.GetParent(baseDataDirectory)?.FullName;

            if (string.IsNullOrEmpty(gameRootPath)) return false;

            string gameExePath = Directory.GetFiles(gameRootPath, "*.exe")
                .FirstOrDefault(exe => !Path.GetFileNameWithoutExtension(exe).StartsWith("SkyRez.UnityPatcher", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(gameExePath)) return false;
            string gameName = Path.GetFileNameWithoutExtension(gameExePath);
            string expectedUnityDataFolderName = gameName + "_Data";

            PathResolver.Initialize(gameRootPath, gameName, expectedUnityDataFolderName);

            string configFileName = "SkyRez.Config.ini";
            string configPathPattern = Path.Combine("{Data}", configFileName);
            string resolvedConfigPath = PathResolver.Resolve(configPathPattern);

            ConfigManager.Load(resolvedConfigPath);
            Logger.Initialize();

            Logger.Information("================================================");
            Logger.Information("    Загрузчик модов SkyRez (PluginEntry)");
            Logger.Information($"    Время инициализации: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            Logger.Information($"    Корневая папка игры: {gameRootPath}");
            Logger.Information($"    Имя игры (из exe): {gameName}");
            Logger.Information($"    Папка данных игры ({{Data}}): {expectedUnityDataFolderName}");
            Logger.Information($"    Путь к файлу конфигурации: {resolvedConfigPath}");
            Logger.Information($"    Конфигурация загружена: {ConfigManager.IsLoaded}");
            Logger.Information($"    Логирование в файл включено: {Logger.IsLoggingToFileEnabled}");
            Logger.Information($"    Версия Runtime: .NET {Environment.Version}");
            Logger.Information("================================================");

            if (!ConfigManager.IsLoaded)
                Logger.Warning("Файл конфигурации не найден или не удалось загрузить. Используются значения по умолчанию.");

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "КРИТИЧЕСКАЯ ОШИБКА при настройке инфраструктуры в PluginEntry.SetupInfrastructure()");
            return false;
        }
    }

    /// <summary>Загружает и инициализирует основной модуль мода.</summary>
    /// <remarks>Создает экземпляр типа, указанного в <see cref="modType"/>, и вызывает его метод OnLoad().</remarks>
    private static void LoadMod()
    {
        Logger.Debug($"LoadMod() - НАЧАЛО. Тип мода: {modType.FullName}");
        try
        {
            if (!typeof(SkyRezMod).IsAssignableFrom(modType))
            {
                Logger.Error($"Тип '{modType.FullName}' не является наследником SkyRezMod. Мод не будет загружен.");
                return;
            }

            Logger.Information($"Создание экземпляра мода: {modType.FullName}...");
            modInstance = (SkyRezMod)Activator.CreateInstance(modType);
            Logger.Debug("Экземпляр мода создан.");

            Logger.Information($"Вызов OnLoad() для мода '{modInstance.Name}' версии {modInstance.Version}...");
            modInstance.OnLoad();
            Logger.Information($"Мод '{modInstance.Name}' успешно загружен и инициализирован.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"КРИТИЧЕСКАЯ ОШИБКА при загрузке или инициализации модуля мода '{modType.FullName}'.");
        }
        Logger.Debug("LoadMod() - КОНЕЦ");
    }

    #endregion
}