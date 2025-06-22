// В SkyRez.Common/Managers/ConfigManager.cs (и аналогично в SkyRez.Common.3.5)
using IniParser;
using IniParser.Model;
using System.IO;

namespace SkyRez.Common.Managers
{
    public static class ConfigManager
    {
        private static IniData _configData;
        public static bool IsLoaded = false; // Ваша переменная была IsLoaded с большой буквы, тут с маленькой для единообразия с другими приватными полями

        public static void Load(string configPath)
        {
            if (!File.Exists(configPath))
            {
                _configData = new IniData();
                IsLoaded = false;
                // Logger?.Warning($"Файл конфигурации не найден по пути: {configPath}"); // Опционально, если Logger уже инициализирован базово
                return;
            }

            try
            {
                var parser = new FileIniDataParser();
                _configData = parser.ReadFile(configPath);
                IsLoaded = true;
            }
            catch (Exception ex)
            {
                _configData = new IniData(); // В случае ошибки парсинга, используем пустую конфигурацию
                IsLoaded = false;
                // Logger?.Error(ex, $"Ошибка чтения файла конфигурации: {configPath}"); // Опционально
            }
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            if (!IsLoaded || _configData == null || !_configData.Sections.ContainsSection("Settings") || !_configData["Settings"].ContainsKey(key))
            {
                return defaultValue;
            }
            if (bool.TryParse(_configData["Settings"][key], out bool result))
            {
                return result;
            }
            return defaultValue;
        }

        public static string GetString(string key, string defaultValue = "")
        {
            // Возвращает "сырую" строку из конфига, без какой-либо обработки плейсхолдеров
            if (!IsLoaded || _configData == null || !_configData.Sections.ContainsSection("Settings") || !_configData["Settings"].ContainsKey(key))
            {
                return defaultValue;
            }
            return _configData["Settings"][key] ?? defaultValue;
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            if (!IsLoaded || _configData == null || !_configData.Sections.ContainsSection("Settings") || !_configData["Settings"].ContainsKey(key))
            {
                return defaultValue;
            }
            if (int.TryParse(_configData["Settings"][key], out int result))
            {
                return result;
            }
            return defaultValue;
        }
    }
}