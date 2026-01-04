namespace News;

public abstract record BaseOptions
{
    private DirectoryInfo? opmlDirectory;
    private DirectoryInfo? imageDirectory;

    public required string OpmlPath { get; init; } = @"C:\Tools\News\opml";
    public DirectoryInfo OpmlDirectory => this.opmlDirectory ??= new(this.OpmlPath);

    public required string ImagePath { get; init; } = @"C:\Tools\News\img";
    public DirectoryInfo ImageDirectory => this.imageDirectory ??= new(this.ImagePath);

    public required string Endpoint { get; init; } = @"C:\Tools\News\Newsmaker.sock";
}
