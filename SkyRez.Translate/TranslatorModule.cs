namespace SkyRez.Translator;

public class TranslatorModule : SkyRezMod
{
    public override string Name => "SkyRez.Translator";
    public override Version Version => new("1.0.0");

    private HarmonyInstance harmony;

    public override void OnLoad()
    {
        Logger.Information("Загрузка мода '" + Name + "' версии " + Version + "...");
        try
        {
            harmony = HarmonyInstance.Create(Name);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.Information("Модуль перевода успешно загружен. Патчи Harmony применены.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Произошла критическая ошибка при загрузке модуля перевода.");
        }
    }

    public override void OnUnload()
    {
        Logger.Information($"Выгрузка мода '{Name}'...");
        if (harmony != null)
        {
            harmony.UnpatchAll(Name);
            Logger.Information("Патчи Harmony отменены.");
        }
    }
}

[HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
internal class TextSetterPatch
{
    static void Prefix(ref string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        Logger.Debug($"Перехвачен текст: \"{value}\"");

        if (value == "New Game")
        {
            value = "Новая игра";
            Logger.Information("Текст 'New Game' заменен на 'Новая игра'");
        }
        else if (value == "Continue")
        {
            value = "Продолжить";
            Logger.Information("Текст 'Continue' заменен на 'Продолжить'");
        }
        else if (value == "Options")
        {
            value = "Настройки";
            Logger.Information("Текст 'Options' заменен на 'Настройки'");
        }
        else if (value == "Exit")
        {
            value = "Выход";
            Logger.Information("Текст 'Exit' заменен на 'Выход'");
        }
    }
}