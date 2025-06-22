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
    /// <remarks>Инициализирует логгер, определяет путь к игре <see cref="FindGameExecutable"/>.<br/>Определяет версию .NET <see cref="GetTargetFrameworkFromAttribute"/> и <see cref="GetTargetFrameworkFromMscorlib"/>.<br/>Определяет архитектуру <see cref="Is64BitExecutable"/>, устанавливает прокси-DLL <see cref="InstallProxyDll"/>.<br/>Создает файл конфигурации <see cref="CreateDefaultConfig"/>.</remarks>
    private static void Main()
    {
        Logger.Initialize();

        Logger.Information("================================================");
        Logger.Information("    Запуск SkyRez Unity Patcher");
        Logger.Information("================================================");

        try
        {
            string? gameExePath = FindGameExecutable();
            if (gameExePath is null)
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
                Logger.Error($"Папка данных игры '{dataFolderName}' не найдена. Убедитесь, что патчер находится в корневой папке игры ({AppContext.BaseDirectory}).");
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
                    Logger.Information($"Атрибут TargetFramework не найден в '{coreAssemblyPath}'. Попытка анализа ссылок на mscorlib...");
                    targetFramework = GetTargetFrameworkFromMscorlib(coreAssemblyPath);
                }
            }
            else Logger.Warning($"Не найдена ключевая сборка '{CoreAssembly}' по пути '{coreAssemblyPath}' для определения версии .NET Framework.");
            Logger.Information($"Целевая версия .NET Framework игры: {targetFramework}");

            string configPath = Path.Combine(dataFolderPath, ConfigFileName);
            string relativeConfigPath = Path.GetRelativePath(AppContext.BaseDirectory, configPath);
            Logger.Information($"Путь к файлу конфигурации: {relativeConfigPath}");
            CreateDefaultConfig(configPath);

            ConfigManager.Load(configPath);
            Logger.Initialize();

            bool is64bit = Is64BitExecutable(gameExePath);
            Logger.Information($"Архитектура игры: {(is64bit ? "x64 (64-бит)" : "x86 (32-бит)")}");
            InstallProxyDll(is64bit);

            string recommendation = targetFramework.Contains("4.x") || targetFramework.Contains("4.0") ? ".NET Framework 4.x (рекомендуется 4.7.2+)" :
                                    targetFramework.Contains("3.5") || targetFramework.Contains("2.0") ? ".NET Framework 3.5" :
                                    targetFramework;

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

    /// <summary>Ищет исполняемый файл игры (.exe) в текущей директории <see cref="AppContext.BaseDirectory"/>, исключая сам патчер.</summary>
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

    /// <summary>Определяет, является ли указанный исполняемый файл 64-битным, анализируя его PE-заголовок.</summary>
    /// <param name="filePath">Путь к исполняемому файлу.</param>
    /// <returns><c>true</c>, если файл 64-битный (машинный код <c>0x8664</c>); в противном случае <c>false</c>.</returns>
    /// <exception cref="Exception">Если файл имеет неверный формат PE (например, отсутствует сигнатура <c>0x00004550</c>).</exception>
    private static bool Is64BitExecutable(string filePath)
    {
        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using BinaryReader binaryReader = new(fileStream);
        _ = fileStream.Seek(0x3c, SeekOrigin.Begin);
        int peOffset = binaryReader.ReadInt32();
        _ = fileStream.Seek(peOffset, SeekOrigin.Begin);
        uint peSignature = binaryReader.ReadUInt32();
        if (peSignature is not 0x00004550)
            throw new Exception($"Неверный формат PE файла '{filePath}'. Сигнатура не соответствует 'PE\\0\\0'.");

        ushort machine = binaryReader.ReadUInt16();
        return machine is 0x8664;
    }

    #endregion

    #region Определение .NET Framework

    /// <summary>Определяет целевую версию .NET Framework сборки путем чтения атрибута <c>System.Runtime.Versioning.TargetFrameworkAttribute</c>.</summary>
    /// <remarks>Это наиболее точный современный способ.<br/>В случае неудачи стоит попробовать <see cref="GetTargetFrameworkFromMscorlib"/>.</remarks>
    /// <param name="assemblyPath">Путь к файлу сборки для анализа.</param>
    /// <returns>Строка, представляющая целевой фреймворк (например, <c>".NETFramework,Version=v4.7.2"</c>).<br/>Возвращает <c>"Нет метаданных .NET"</c> если метаданные отсутствуют.<br/>Возвращает <c>"Не найдено"</c> если атрибут не найден или произошла ошибка.</returns>
    private static string GetTargetFrameworkFromAttribute(string assemblyPath)
    {
        try
        {
            using FileStream fs = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using PEReader peReader = new(fs);
            if (!peReader.HasMetadata)
                return "Нет метаданных .NET";

            MetadataReader reader = peReader.GetMetadataReader();
            AssemblyDefinition assemblyDef = reader.GetAssemblyDefinition();

            foreach (CustomAttributeHandle handle in assemblyDef.GetCustomAttributes())
            {
                CustomAttribute attribute = reader.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind is not HandleKind.MemberReference) continue;

                MemberReference memberRef = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (memberRef.Parent.Kind is not HandleKind.TypeReference) continue;

                TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                if (reader.GetString(typeRef.Name) is "TargetFrameworkAttribute" &&
                    reader.GetString(typeRef.Namespace) is "System.Runtime.Versioning")
                {
                    CustomAttributeValue<string> value = attribute.DecodeValue(new SimpleStringProvider());
                    return value.FixedArguments.Length > 0 && value.FixedArguments[0].Value is string valStr
                        ? valStr
                        : "N/A (атрибут TargetFrameworkAttribute найден, но значение не строка)";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, $"Ошибка при чтении TargetFrameworkAttribute из '{Path.GetFileName(assemblyPath)}'.");
        }
        return "Не найдено";
    }

    /// <summary>Определяет целевую версию .NET Framework сборки путем анализа версии ссылочной сборки <c>mscorlib.dll</c>.</summary>
    /// <remarks>Используется как запасной вариант, если <see cref="GetTargetFrameworkFromAttribute"/> не дал результата, особенно для старых игр.</remarks>
    /// <param name="assemblyPath">Путь к файлу сборки для анализа.</param>
    /// <returns>Строка, описывающая профиль .NET (например, <c>".NET 4.x Profile"</c>, <c>".NET 3.5 Profile"</c>).<br/>Возвращает <c>"Нет метаданных .NET"</c> если метаданные отсутствуют.<br/>Возвращает <c>"Не найдено"</c> если ссылка на mscorlib не найдена или произошла ошибка.</returns>
    private static string GetTargetFrameworkFromMscorlib(string assemblyPath)
    {
        try
        {
            using FileStream fs = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using PEReader peReader = new(fs);
            if (!peReader.HasMetadata)
                return "Нет метаданных .NET";

            MetadataReader reader = peReader.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
            {
                AssemblyReference reference = reader.GetAssemblyReference(handle);
                if (reader.GetString(reference.Name) is "mscorlib")
                {
                    Version version = reference.Version;
                    return version.Major switch
                    {
                        4 => $".NET 4.x Profile (mscorlib v{version})",
                        2 => $".NET 3.5 Profile (mscorlib v{version})",
                        _ => $"Неизвестная версия mscorlib: {version}"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, $"Ошибка при анализе ссылки на mscorlib из '{Path.GetFileName(assemblyPath)}'.");
        }
        return "Не найдено";
    }

    #endregion

    #region Установка прокси-DLL

    /// <summary>Устанавливает прокси-DLL (имя файла определяется <see cref="ProxyDllName"/>) в корневую директорию игры <see cref="AppContext.BaseDirectory"/>.</summary>
    /// <remarks>DLL извлекается из встроенных ресурсов патчера в зависимости от архитектуры игры.</remarks>
    /// <param name="is64bit"><c>true</c>, если целевая игра 64-битная (ресурс <c>"SkyRez.UnityPatcher.Resources.x64.version.dll"</c>).<br/><c>false</c>, если 32-битная (ресурс <c>"SkyRez.UnityPatcher.Resources.x86.version.dll"</c>).</param>
    /// <exception cref="FileNotFoundException">Если не удалось найти встроенный ресурс DLL. Проверьте имена ресурсов и настройки сборки.</exception>
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
                throw new FileNotFoundException($"Не удалось найти встроенный ресурс DLL: {resourceName}. Убедитесь, что DLL правильно встроены в патчер как EmbeddedResource.");
            }
            using FileStream fileStream = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.CopyTo(fileStream);
        }
        Logger.Information($"Прокси-библиотека '{ProxyDllName}' успешно установлена в: {targetPath}");
    }

    #endregion

    #region Управление конфигурацией

    /// <summary>Создает или перезаписывает файл конфигурации (имя файла <see cref="ConfigFileName"/>) в папке данных игры.</summary>
    /// <remarks>Содержимое файла конфигурации извлекается из встроенного ресурса <c>"SkyRez.UnityPatcher.Resources.DefaultConfig.ini"</c>.</remarks>
    /// <param name="configPath">Полный путь, по которому должен быть создан файл конфигурации.</param>
    /// <exception cref="FileNotFoundException">Если не удалось найти встроенный ресурс конфигурации. Проверьте имя ресурса и настройки сборки.</exception>
    private static void CreateDefaultConfig(string configPath)
    {
        string? dirName = Path.GetDirectoryName(configPath);
        string relativeDir = ".";
        if (dirName is not null)
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

            if (dirName is not null && !Directory.Exists(dirName))
                _ = Directory.CreateDirectory(dirName);

            File.WriteAllText(configPath, defaultConfigContent);
        }
        Logger.Information($"Файл '{Path.GetFileName(configPath)}' успешно создан/перезаписан.");
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>Выводит сообщение об ожидании нажатия клавиши и завершает работу приложения.</summary>
    /// <remarks>Использует <see cref="Console.ReadKey()"/>.</remarks>
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

    /// <summary>Вспомогательный класс для декодирования значений пользовательских атрибутов из метаданных сборки.</summary>
    /// <remarks>Реализует интерфейс <see cref="ICustomAttributeTypeProvider{TType}"/> для <see cref="System.Reflection.Metadata"/>.</remarks>
    private class SimpleStringProvider : ICustomAttributeTypeProvider<string>
    {
        /// <summary>Возвращает строковое представление примитивного типа.</summary>
        /// <param name="typeCode">Код примитивного типа из перечисления <see cref="PrimitiveTypeCode"/>.</param>
        /// <returns>Строковое представление имени типа (например, <c>"Int32"</c>, <c>"String"</c>).</returns>
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        /// <summary>Возвращает строковое представление для типа <see cref="System.Type"/>.</summary>
        /// <returns>Строка <c>"System.Type"</c>.</returns>
        public string GetSystemType() => "System.Type";

        /// <summary>Возвращает имя типа из его определения в метаданных.</summary>
        /// <param name="reader">Экземпляр <see cref="MetadataReader"/> для доступа к метаданным.</param>
        /// <param name="handle">Дескриптор определения типа <see cref="TypeDefinitionHandle"/>.</param>
        /// <param name="rawTypeKind">Байт, представляющий "сырой" вид типа (не используется в данной реализации).</param>
        /// <returns>Полное имя типа (включая пространство имен), извлеченное из метаданных <see cref="TypeDefinition"/>.</returns>
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            TypeDefinition typeDef = reader.GetTypeDefinition(handle);
            string name = reader.GetString(typeDef.Name);
            string ns = reader.GetString(typeDef.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        /// <summary>Возвращает имя типа из его ссылки в метаданных.</summary>
        /// <param name="reader">Экземпляр <see cref="MetadataReader"/> для доступа к метаданным.</param>
        /// <param name="handle">Дескриптор ссылки на тип <see cref="TypeReferenceHandle"/>.</param>
        /// <param name="rawTypeKind">Байт, представляющий "сырой" вид типа (не используется в данной реализации).</param>
        /// <returns>Полное имя типа (включая пространство имен), извлеченное из ссылки <see cref="TypeReference"/> в метаданных.</returns>
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference typeRef = reader.GetTypeReference(handle);
            string name = reader.GetString(typeRef.Name);
            string ns = reader.GetString(typeRef.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        /// <summary>Возвращает строковое представление для спецификации типа (например, массив или generic-тип).</summary>
        /// <remarks>В данной реализации возвращает просто <c>"TypeSpec"</c>.</remarks>
        /// <param name="reader">Экземпляр <see cref="MetadataReader"/> (не используется в данной реализации).</param>
        /// <param name="handle">Дескриптор спецификации типа <see cref="TypeSpecificationHandle"/> (не используется в данной реализации).</param>
        /// <param name="rawTypeKind">Байт, представляющий "сырой" вид типа (не используется в данной реализации).</param>
        /// <returns>Строка <c>"TypeSpec"</c>.</returns>
        public string GetTypeFromSpecification(MetadataReader reader, TypeSpecificationHandle handle, byte rawTypeKind) => "TypeSpec";

        /// <summary>Возвращает строковое представление для типа одномерного массива.</summary>
        /// <param name="elementType">Строковое представление типа элемента массива.</param>
        /// <returns>Строка вида <c>"elementType[]"</c>.</returns>
        public string GetSZArrayType(string elementType) => elementType + "[]";

        /// <summary>Возвращает строковое представление для generic-типа с его аргументами.</summary>
        /// <param name="genericType">Строковое представление общего (generic) типа.</param>
        /// <param name="typeArguments">Неизменяемый массив строк <see cref="ImmutableArray{T}"/>, представляющих аргументы типа.</param>
        /// <returns>Строка вида <c>"genericType&lt;arg1,arg2,...&gt;"</c>.</returns>
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(",", typeArguments)}>";

        /// <summary>Возвращает тип из его сериализованного имени.</summary>
        /// <param name="name">Сериализованное имя типа.</param>
        /// <returns>Переданное имя типа без изменений.</returns>
        public string GetTypeFromSerializedName(string name) => name;

        /// <summary>Возвращает базовый примитивный тип для перечисления (enum).</summary>
        /// <param name="enumType">Строковое представление типа перечисления (не используется в данной реализации).</param>
        /// <returns><see cref="PrimitiveTypeCode.Int32"/> по умолчанию.</returns>
        public PrimitiveTypeCode GetUnderlyingEnumType(string enumType) => PrimitiveTypeCode.Int32;

        /// <summary>Проверяет, является ли переданная строка типом <see cref="System.Type"/>.</summary>
        /// <param name="type">Строка, представляющая имя типа.</param>
        /// <returns><c>true</c>, если строка равна <c>"System.Type"</c>; иначе <c>false</c>.</returns>
        public bool IsSystemType(string type) => type is "System.Type";
    }

    #endregion
}