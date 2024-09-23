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

        var infoVersion = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;

        var metadataPos = infoVersion.IndexOf('+');
        if (metadataPos > 0)
        {
            infoVersion = infoVersion[..metadataPos];
        }

        var fileVersion = assembly?
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version ?? string.Empty;

        version = string.IsNullOrWhiteSpace(fileVersion) ? infoVersion : $"{infoVersion} ({fileVersion})";
    }

    public string Product => product;
    public string Version => version;
}
