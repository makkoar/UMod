namespace SkyRez.Common.Resolvers;

public static class PathResolver
{
    public static bool IsInitialized;
    private static string GameRootPath = string.Empty;
    private static string GameName = string.Empty;
    private static string DataFolderName = string.Empty;

    /// <summary>
    /// Инициализирует распознаватель путей. Должен быть вызван один раз при старте мода.
    /// </summary>
    public static void Initialize(string gameRootPath, string gameName, string dataFolderName)
    {
        GameRootPath = gameRootPath;
        GameName = gameName;
        DataFolderName = dataFolderName;
        IsInitialized = true;
    }

    /// <summary>
    /// Преобразует путь с плейсхолдерами в полный абсолютный путь.
    /// </summary>
    public static string Resolve(string pathWithPlaceholders)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("PathResolver не был инициализирован. Вызовите Initialize() при старте мода.");

        // Если путь уже абсолютный, возвращаем его как есть.
        if (Path.IsPathFullyQualified(pathWithPlaceholders))
            return pathWithPlaceholders;

        StringBuilder sb = new StringBuilder(pathWithPlaceholders)
            .Replace("{Name}", GameName)
            .Replace("{Data}", DataFolderName);

        return Path.Combine(GameRootPath, sb.ToString());
    }
}