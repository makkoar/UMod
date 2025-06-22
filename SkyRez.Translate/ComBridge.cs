namespace SkyRez.Translate;

/// <summary>Представляет COM-видимый мост для взаимодействия с неуправляемым кодом загрузчика.</summary>
/// <remarks>Этот класс используется для инициализации .NET части мода из C++ proxy DLL.</remarks>
[ComVisible(true)]
[Guid("a90a7f4a-ef2c-419f-ad55-877d895d344c")]
public class ComBridge
{
    #region Открытые методы

    /// <summary>Точка входа, вызываемая из C++ proxy DLL для инициализации .NET мода.</summary>
    /// <remarks>Этот метод вызывает <see cref="PluginEntry.Initialize"/> для настройки и загрузки мода.</remarks>
    [ComVisible(true)]
    public void Bootstrap()
    {
        try
        {
            PluginEntry.Initialize();
        }
        catch (Exception ex)
        {
            // Попытка записать критическую ошибку, если Logger еще не инициализирован или не работает
            // Это аварийный механизм, основной лог должен быть через Logger в PluginEntry
            try
            {
                string emergencyLogDir = AppDomain.CurrentDomain.BaseDirectory; // [GameName]_Data/
                if (Directory.GetParent(emergencyLogDir) != null)
                    emergencyLogDir = Directory.GetParent(emergencyLogDir).FullName; // Game Root
                string emergencyLogPath = Path.Combine(emergencyLogDir, "SKYREZ_FATAL_BOOTSTRAP_ERROR.txt");
                File.AppendAllText(emergencyLogPath, $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss.fff}] КРИТИЧЕСКАЯ ОШИБКА в ComBridge.Bootstrap (до возможной активации логгера):\r\n{ex}\r\n");
            }
            catch
            {
                // Если даже аварийный лог не удался, ничего не поделать
            }
        }
    }

    #endregion
}