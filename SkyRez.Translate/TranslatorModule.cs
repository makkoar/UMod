namespace SkyRez.Translator;

/// <summary>Основной класс мода-переводчика.</summary>
/// <remarks>Отвечает за применение патчей для перехвата и замены текста в UI.</remarks>
public class TranslatorModule : SkyRezMod
{
    #region Приватные поля

    /// <summary>Экземпляр Harmony для применения патчей.</summary>
    /// <value>Инициализируется в <see cref="OnLoad"/>.</value>
    private HarmonyInstance harmony;

    #endregion

    #region Переопределения SkyRezMod

    /// <summary>Получает уникальное имя мода.</summary>
    /// <value>"SkyRez.Translator"</value>
    public override string Name => "SkyRez.Translator";

    /// <summary>Получает версию мода.</summary>
    /// <value>Новая версия System.Version("1.0.0").</value>
    public override Version Version => new("1.0.0");

    /// <summary>Метод, вызываемый при загрузке мода.</summary>
    /// <remarks>Инициализирует Harmony и применяет все патчи из текущей сборки.</remarks>
    public override void OnLoad()
    {
        Logger.Information($"Загрузка модуля '{Name}' версии {Version}...");
        try
        {
            harmony = HarmonyInstance.Create(Name);
            Logger.Debug($"HarmonyInstance создан с ID: {Name}");

            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.Information("Все патчи Harmony из сборки применены.");

            Logger.Information($"Модуль '{Name}' успешно загружен.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"КРИТИЧЕСКАЯ ОШИБКА при загрузке модуля '{Name}'.");
        }
    }

    /// <summary>Метод, вызываемый при выгрузке мода (если будет реализована).</summary>
    /// <remarks>Отменяет все патчи, примененные этим экземпляром Harmony.</remarks>
    public override void OnUnload()
    {
        Logger.Information($"Выгрузка модуля '{Name}'...");
        if (harmony != null)
        {
            try
            {
                harmony.UnpatchAll(Name);
                Logger.Information("Все патчи Harmony были успешно отменены.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Ошибка при отмене патчей Harmony для модуля '{Name}'.");
            }
        }
        else
        {
            Logger.Warning("Экземпляр Harmony не был инициализирован, отмена патчей невозможна.");
        }
    }

    #endregion
}

/// <summary>Патч для сеттера свойства <c>text</c> класса <see cref="UnityEngine.UI.Text"/>.</summary>
/// <remarks>Перехватывает установку текста и заменяет его при необходимости.</remarks>
[HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
internal static class TextSetterPatch
{
    #region Патчи Harmony

    /// <summary>Префикс-метод для патча.</summary>
    /// <param name="value">Ссылка на значение, присваиваемое свойству <c>text</c>. Может быть изменено.</param>
    /// <remarks>Этот метод выполняется перед оригинальным сеттером свойства.</remarks>
    static void Prefix(ref string value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        string originalValue = value;

        switch (value)
        {
            case "New Game":
                value = "Новая игра";
                Logger.Information($"Текст '{originalValue}' заменен на '{value}'.");
                break;
            case "Continue":
                value = "Продолжить";
                Logger.Information($"Текст '{originalValue}' заменен на '{value}'.");
                break;
            case "Options":
                value = "Настройки";
                Logger.Information($"Текст '{originalValue}' заменен на '{value}'.");
                break;
            case "Exit":
                value = "Выход";
                Logger.Information($"Текст '{originalValue}' заменен на '{value}'.");
                break;
            default:
                // Logger.Debug($"Текст \"{originalValue}\" не требует замены."); // Раскомментировать для отладки всех текстов
                break;
        }
    }

    #endregion
}