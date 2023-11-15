namespace News.App.Services;

using System.Reflection;

public interface IApp
{
    string Product { get; }
    string Version { get; }
}

class AppService : IApp
{
    private static readonly string product;
    private static readonly string version;

    static AppService()
    {
        var assembly = Assembly.GetEntryAssembly();

        product = assembly?
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product ?? string.Empty;

        version = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;

        var metadataPos = version.IndexOf('+');
        if (metadataPos > 0)
        {
            version = version[..metadataPos];
        }
    }

    public string Product => product;
    public string Version => version;
}
