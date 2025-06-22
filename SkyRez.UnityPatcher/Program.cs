namespace SkyRez.UnityPatcher;

/// <summary>Главный класс утилиты патчинга игр на движке Unity для SkyRez.</summary>
internal class Program
{
    #region Константы

    /// <summary>Имя файла конфигурации, используемое патчером и модом.</summary>
    /// <value>"SkyRez.Config.ini"</value>
    private const string ConfigFileName = "SkyRez.Config.ini";

    /// <summary>Имя прокси-DLL, которая внедряется в игру.</summary>
    /// <value>"version.dll"</value>
    private const string ProxyDllName = "version.dll";

    /// <summary>Имя основной сборки Unity-игры, используемой для определения версии .NET Framework.</summary>
    /// <value>"Assembly-CSharp.dll"</value>
    private const string CoreAssembly = "Assembly-CSharp.dll";

    #endregion

    #region Главная точка входа

    /// <summary>Главная точка входа для приложения SkyRez Unity Patcher.</summary>
    /// <remarks>Инициализирует логгер, определяет путь к игре, версию .NET, архитектуру,<br/>устанавливает прокси-DLL и создает файл конфигурации.</remarks>
    private static void Main()
    {
        Logger.Initialize(); // Первичная инициализация логгера (без конфига)

        Logger.Information("================================================");
        Logger.Information("    Запуск SkyRez Unity Patcher");
        Logger.Information("================================================");

        try
        {
            string? gameExePath = FindGameExecutable();
            if (string.IsNullOrEmpty(gameExePath))
            {
                Logger.Error("Исполняемый файл игры (.exe) не найден в текущей директории.");
                PauseAndExit();
                return;
            }
            string gameName = Path.GetFileName(gameExePath);
            Logger.Information($"Обнаружен исполняемый файл игры: {gameName}");

            string gameNameWithoutExt = Path.GetFileNameWithoutExtension(gameExePath);
            string dataFolderName = $"{gameNameWithoutExt}_Data";
            string dataFolderPath = Path.Combine(AppContext.BaseDirectory, dataFolderName);
            if (!Directory.Exists(dataFolderPath))
            {
                Logger.Error($"Папка данных игры '{dataFolderName}' не найдена. Убедитесь, что патчер находится в корневой папке игры.");
                PauseAndExit();
                return;
            }

            string coreAssemblyPath = Path.Combine(dataFolderPath, "Managed", CoreAssembly);
            string targetFramework = "Не определено";
            if (File.Exists(coreAssemblyPath))
            {
                targetFramework = GetTargetFrameworkFromAttribute(coreAssemblyPath);
                if (targetFramework is "Не найдено")
                {
                    Logger.Information("Атрибут TargetFramework не найден. Попытка анализа ссылок на mscorlib...");
                    targetFramework = GetTargetFrameworkFromMscorlib(coreAssemblyPath);
                }
            }
            else Logger.Warning($"Не найдена ключевая сборка '{CoreAssembly}' для определения версии .NET Framework.");
            Logger.Information($"Целевая версия .NET Framework игры: {targetFramework}");

            string configPath = Path.Combine(dataFolderPath, ConfigFileName);
            string relativeConfigPath = Path.GetRelativePath(AppContext.BaseDirectory, configPath);
            Logger.Information($"Путь к файлу конфигурации: {relativeConfigPath}");
            CreateDefaultConfig(configPath); // Создает/перезаписывает конфиг

            ConfigManager.Load(configPath); // Загружаем созданный конфиг
            Logger.Initialize(); // Переинициализация логгера с учетом настроек из файла конфигурации

            bool is64bit = Is64BitExecutable(gameExePath);
            Logger.Information($"Архитектура игры: {(is64bit ? "x64 (64-бит)" : "x86 (32-бит)")}");
            InstallProxyDll(is64bit);

            string recommendation = targetFramework.Contains("4.x") || targetFramework.Contains("4.0") ? ".NET Framework 4.x (рекомендуется 4.7.2+)" :
                                    targetFramework.Contains("3.5") || targetFramework.Contains("2.0") ? ".NET Framework 3.5" :
                                    targetFramework; // Если ".NETStandard" или другое

            Logger.Information("================================================");
            Logger.Information("    Патчинг успешно завершен!");
            Logger.Information($"    Рекомендуемая версия для сборки мода: {recommendation}");
            Logger.Information("================================================");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Во время патчинга произошла критическая ошибка!");
        }

        PauseAndExit();
    }

    #endregion

    #region Обнаружение и анализ игры

    /// <summary>Ищет исполняемый файл игры (.exe) в текущей директории, исключая сам патчер.</summary>
    /// <returns>Полный путь к найденному исполняемому файлу игры или <c>null</c>, если файл не найден.</returns>
    /// <exception cref="InvalidOperationException">Если не удалось определить имя сборки патчера.</exception>
    private static string? FindGameExecutable()
    {
        string? patcherAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        if (string.IsNullOrEmpty(patcherAssemblyName))
        {
            Logger.Error("Не удалось определить имя сборки патчера.");
            throw new InvalidOperationException("Не удалось определить имя сборки патчера.");
        }
        string[] exeFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.exe");
        return exeFiles.FirstOrDefault(filePath =>
            !Path.GetFileNameWithoutExtension(filePath).Equals(patcherAssemblyName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Определяет, является ли указанный исполняемый файл 64-битным.</summary>
    /// <param name="filePath">Путь к исполняемому файлу.</param>
    /// <returns><c>true</c>, если файл 64-битный; в противном случае <c>false</c>.</returns>
    /// <exception cref="Exception">Если файл имеет неверный формат PE.</exception>
    private static bool Is64BitExecutable(string filePath)
    {
        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using BinaryReader binaryReader = new(fileStream);
        _ = fileStream.Seek(0x3c, SeekOrigin.Begin);
        int peOffset = binaryReader.ReadInt32();
        _ = fileStream.Seek(peOffset, SeekOrigin.Begin);
        uint peSignature = binaryReader.ReadUInt32();
        if (peSignature != 0x00004550) throw new Exception("Неверный формат PE файла. Сигнатура не соответствует 'PE\\0\\0'.");
        ushort machine = binaryReader.ReadUInt16();
        return machine is 0x8664;
    }

    #endregion

    #region Определение .NET Framework

    /// <summary>Определяет целевую версию .NET Framework сборки путем чтения атрибута <c>TargetFrameworkAttribute</c>.<br/>Это наиболее точный современный способ.</summary>
    /// <param name="assemblyPath">Путь к файлу сборки для анализа.</param>
    /// <returns>Строка, представляющая целевой фреймворк (например, ".NETFramework,Version=v4.7.2"),<br/>"Нет метаданных .NET" если метаданные отсутствуют,<br/>или "Не найдено" если атрибут не найден или произошла ошибка.</returns>
    private static string GetTargetFrameworkFromAttribute(string assemblyPath)
    {
        try
        {
            using FileStream fs = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using PEReader peReader = new(fs);
            if (!peReader.HasMetadata)
            {
                return "Нет метаданных .NET";
            }

            MetadataReader reader = peReader.GetMetadataReader();
            AssemblyDefinition assemblyDef = reader.GetAssemblyDefinition();

            foreach (CustomAttributeHandle handle in assemblyDef.GetCustomAttributes())
            {
                CustomAttribute attribute = reader.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;

                MemberReference memberRef = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (memberRef.Parent.Kind != HandleKind.TypeReference) continue;

                TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                if (reader.GetString(typeRef.Name) is "TargetFrameworkAttribute" &&
                    reader.GetString(typeRef.Namespace) is "System.Runtime.Versioning")
                {
                    CustomAttributeValue<string> value = attribute.DecodeValue(new SimpleStringProvider());
                    return value.FixedArguments.Length > 0 && value.FixedArguments[0].Value is string valStr
                        ? valStr
                        : "N/A (атрибут найден, но значение не строка)";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, $"Ошибка при чтении TargetFrameworkAttribute из '{assemblyPath}'.");
        }
        return "Не найдено";
    }

    /// <summary>Определяет целевую версию .NET Framework сборки путем анализа версии ссылочной сборки <c>mscorlib.dll</c>.<br/>Используется как запасной вариант для старых игр, где атрибут <c>TargetFrameworkAttribute</c> может отсутствовать.</summary>
    /// <param name="assemblyPath">Путь к файлу сборки для анализа.</param>
    /// <returns>Строка, описывающая профиль .NET (например, ".NET 4.x Profile", ".NET 3.5 Profile"),<br/>"Нет метаданных .NET" если метаданные отсутствуют,<br/>или "Не найдено" если ссылка на mscorlib не найдена или произошла ошибка.</returns>
    private static string GetTargetFrameworkFromMscorlib(string assemblyPath)
    {
        try
        {
            using FileStream fs = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using PEReader peReader = new(fs);
            if (!peReader.HasMetadata)
            {
                return "Нет метаданных .NET";
            }

            MetadataReader reader = peReader.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
            {
                AssemblyReference reference = reader.GetAssemblyReference(handle);
                if (reader.GetString(reference.Name) is "mscorlib")
                {
                    Version version = reference.Version;
                    return version.Major switch
                    {
                        4 => ".NET 4.x Profile (mscorlib v" + version.ToString() + ")",
                        2 => ".NET 3.5 Profile (mscorlib v" + version.ToString() + ")",
                        _ => $"Неизвестная версия mscorlib: {version}"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, $"Ошибка при анализе ссылки на mscorlib из '{assemblyPath}'.");
        }
        return "Не найдено";
    }

    #endregion

    #region Установка прокси-DLL

    /// <summary>Устанавливает прокси-DLL (version.dll) в корневую директорию игры.<br/>DLL извлекается из встроенных ресурсов патчера в зависимости от архитектуры игры.</summary>
    /// <param name="is64bit"><c>true</c>, если целевая игра 64-битная; <c>false</c>, если 32-битная.</param>
    /// <exception cref="FileNotFoundException">Если не удалось найти встроенный ресурс DLL.</exception>
    private static void InstallProxyDll(bool is64bit)
    {
        string resourceName = $"SkyRez.UnityPatcher.Resources.{(is64bit ? "x64" : "x86")}.version.dll";
        string targetPath = Path.Combine(AppContext.BaseDirectory, ProxyDllName);

        Logger.Information($"Извлечение ресурса прокси-DLL: {resourceName}");
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream is null)
            {
                Logger.Error($"Критическая ошибка: не удалось найти встроенный ресурс DLL: {resourceName}.");
                throw new FileNotFoundException($"Не удалось найти встроенный ресурс DLL: {resourceName}. Убедитесь, что DLL правильно встроены в патчер.");
            }
            using FileStream fileStream = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.CopyTo(fileStream);
        }
        Logger.Information($"Прокси-библиотека '{ProxyDllName}' успешно установлена в: {targetPath}");
    }

    #endregion

    #region Управление конфигурацией

    /// <summary>Создает или перезаписывает файл конфигурации <code>SkyRez.Config.ini</code> в папке данных игры.<br/>Содержимое файла конфигурации извлекается из встроенного ресурса.</summary>
    /// <param name="configPath">Полный путь, по которому должен быть создан файл конфигурации.</param>
    /// <exception cref="FileNotFoundException">Если не удалось найти встроенный ресурс конфигурации.</exception>
    private static void CreateDefaultConfig(string configPath)
    {
        string? dirName = Path.GetDirectoryName(configPath);
        string relativeDir = ".";
        if (dirName != null)
            relativeDir = Path.GetRelativePath(AppContext.BaseDirectory, dirName);

        Logger.Information($"Создание/перезапись файла конфигурации в '{Path.Combine(relativeDir, Path.GetFileName(configPath))}'...");
        const string configResourceName = "SkyRez.UnityPatcher.Resources.DefaultConfig.ini";
        Assembly assembly = Assembly.GetExecutingAssembly();

        using (Stream? stream = assembly.GetManifestResourceStream(configResourceName))
        {
            if (stream is null)
            {
                Logger.Error($"Критическая ошибка: не найден встроенный ресурс конфигурации '{configResourceName}'.");
                throw new FileNotFoundException($"Критическая ошибка: не найден встроенный ресурс конфигурации '{configResourceName}'. Файл конфигурации не будет создан.");
            }
            using StreamReader reader = new(stream);
            string defaultConfigContent = reader.ReadToEnd();

            if (dirName != null && !Directory.Exists(dirName))
            {
                _ = Directory.CreateDirectory(dirName);
                Logger.Information($"Создана директория для конфигурации: {dirName}");
            }
            File.WriteAllText(configPath, defaultConfigContent);
        }
        Logger.Information($"Файл '{Path.GetFileName(configPath)}' успешно создан/перезаписан.");
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>Выводит сообщение об ожидании нажатия клавиши и завершает работу приложения.</summary>
    private static void PauseAndExit()
    {
        Logger.Information("Нажмите любую клавишу для выхода...");
        try
        {
            _ = Console.ReadKey();
        }
        catch (InvalidOperationException)
        {
            Logger.Warning("Не удалось выполнить Console.ReadKey(). Завершение работы.");
        }
    }

    #endregion

    #region Внутренние вспомогательные классы

    /// <summary>Вспомогательный класс для декодирования значений пользовательских атрибутов из метаданных сборки.<br/>Реализует интерфейс <c>ICustomAttributeTypeProvider<string></c> для <c>System.Reflection.Metadata</c>.</summary>
    private class SimpleStringProvider : ICustomAttributeTypeProvider<string>
    {
        /// <summary>Возвращает строковое представление примитивного типа.</summary>
        /// <param name="typeCode">Код примитивного типа из перечисления <code>PrimitiveTypeCode</code>.</param>
        /// <returns>Строковое представление имени типа (например, "Int32", "String").</returns>
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        /// <summary>Возвращает строковое представление для типа <code>System.Type</code>.</summary>
        /// <returns>Строка "System.Type".</returns>
        public string GetSystemType() => "System.Type";

        /// <summary>Возвращает имя типа из его определения в метаданных.</summary>
        /// <param name="reader">Экземпляр <code>MetadataReader</code> для доступа к метаданным.</param>
        /// <param name="handle">Дескриптор определения типа (<code>TypeDefinitionHandle</code>).</param>
        /// <param name="rawTypeKind">Байт, представляющий "сырой" вид типа (не используется в данной реализации).</param>
        /// <returns>Полное имя типа, извлеченное из метаданных.</returns>
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            TypeDefinition typeDef = reader.GetTypeDefinition(handle);
            string name = reader.GetString(typeDef.Name);
            string ns = reader.GetString(typeDef.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        /// <summary>Возвращает имя типа из его ссылки в метаданных.</summary>
        /// <param name="reader">Экземпляр <code>MetadataReader</code> для доступа к метаданным.</param>
        /// <param name="handle">Дескриптор ссылки на тип (<code>TypeReferenceHandle</code>).</param>
        /// <param name="rawTypeKind">Байт, представляющий "сырой" вид типа (не используется в данной реализации).</param>
        /// <returns>Полное имя типа, извлеченное из ссылки в метаданных.</returns>
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference typeRef = reader.GetTypeReference(handle);
            string name = reader.GetString(typeRef.Name);
            string ns = reader.GetString(typeRef.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        /// <summary>Возвращает строковое представление для спецификации типа (например, массив или generic-тип).</summary>
        /// <param name="reader">Экземпляр <code>MetadataReader</code> (не используется в данной реализации).</param>
        /// <param name="handle">Дескриптор спецификации типа (<code>TypeSpecificationHandle</code>) (не используется в данной реализации).</param>
        /// <param name="rawTypeKind">Байт, представляющий "сырой" вид типа (не используется в данной реализации).</param>
        /// <returns>Строка "TypeSpec".</returns>
        public string GetTypeFromSpecification(MetadataReader reader, TypeSpecificationHandle handle, byte rawTypeKind) => "TypeSpec";

        /// <summary>Возвращает строковое представление для типа одномерного массива.</summary>
        /// <param name="elementType">Строковое представление типа элемента массива.</param>
        /// <returns>Строка вида "elementType[]".</returns>
        public string GetSZArrayType(string elementType) => elementType + "[]";

        /// <summary>Возвращает строковое представление для generic-типа с его аргументами.</summary>
        /// <param name="genericType">Строковое представление общего (generic) типа.</param>
        /// <param name="typeArguments">Неизменяемый массив строк, представляющих аргументы типа.</param>
        /// <returns>Строка вида "genericType<arg1,arg2,...>".</returns>
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(",", typeArguments)}>";

        /// <summary>Возвращает тип из его сериализованного имени.</summary>
        /// <param name="name">Сериализованное имя типа.</param>
        /// <returns>Переданное имя типа без изменений.</returns>
        public string GetTypeFromSerializedName(string name) => name;

        /// <summary>Возвращает базовый примитивный тип для перечисления (enum).</summary>
        /// <param name="enumType">Строковое представление типа перечисления (не используется в данной реализации).</param>
        /// <returns><code>PrimitiveTypeCode.Int32</code> по умолчанию.</returns>
        public PrimitiveTypeCode GetUnderlyingEnumType(string enumType) => PrimitiveTypeCode.Int32;

        /// <summary>Проверяет, является ли переданная строка типом <code>System.Type</code>.</summary>
        /// <param name="type">Строка, представляющая имя типа.</param>
        /// <returns><code>true</code>, если строка равна "System.Type"; иначе <code>false</code>.</returns>
        public bool IsSystemType(string type) => type == "System.Type";
    }

    #endregion
}