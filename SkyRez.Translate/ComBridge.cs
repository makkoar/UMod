namespace SkyRez.Translate;

[ComVisible(true)]
[Guid("a90a7f4a-ef2c-419f-ad55-877d895d344c")]
public class ComBridge
{
    [ComVisible(true)]
    public void Bootstrap()
    {
        try
        {
            PluginEntry.Initialize();
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName, "BOOTSTRAP_ERROR.txt"),
                "Error in ComBridge.Bootstrap: " + ex.ToString()
            );
        }
    }
}