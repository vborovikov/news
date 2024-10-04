namespace News;

using System.Reflection;

public interface IApp
{
    string Name { get; }
    string Product { get; }
    string Version { get; }
    string InfoVersion { get; }
    string FileVersion { get; }
    string UserAgent { get; }
}

public class AppService : IApp
{
    private static readonly string name;
    private static readonly string product;
    private static readonly string version;
    private static readonly string infoVersion;
    private static readonly string fileVersion;

    static AppService()
    {
        var assembly = Assembly.GetEntryAssembly();

        name = assembly?
            .GetCustomAttribute<AssemblyTitleAttribute>()?
            .Title ?? string.Empty;

        product = assembly?
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product ?? string.Empty;

        infoVersion = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;

        var metadataPos = infoVersion.IndexOf('+');
        if (metadataPos > 0)
        {
            infoVersion = infoVersion[..metadataPos];
        }

        fileVersion = assembly?
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version ?? string.Empty;

        version = string.IsNullOrWhiteSpace(fileVersion) ? infoVersion : $"{infoVersion} ({fileVersion})";
    }

    public static readonly AppService Instance = new();

    public string Name => name;
    public string Product => product;
    public string Version => version;
    public string InfoVersion => infoVersion;
    public string FileVersion => fileVersion;

    public string UserAgent => $"{this.Name}/{this.FileVersion} ({this.Product} {this.InfoVersion})";
}
