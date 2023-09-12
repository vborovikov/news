namespace News.Service.Data;

using System;
using System.Threading.Tasks;
using Dodkin;
using Dodkin.Dispatch;
using Microsoft.Extensions.Options;

sealed class UserAgent
{
    private static readonly MessageEndpoint serviceEndpoint = MessageEndpoint.FromName(ServiceOptions.ServiceName.ToLowerInvariant());

    private readonly ServiceOptions options;
    private readonly IQueueRequestDispatcher dispatcher;
    private readonly ILogger<UserAgent> log;

    static UserAgent()
    {
        serviceEndpoint.CreateIfNotExists(ServiceOptions.ServiceName);
    }

    public UserAgent(IOptions<ServiceOptions> options, ILogger<UserAgent> log)
    {
        this.options = options.Value;
        this.dispatcher = new QueueRequestDispatcher(MessageQueueName.Parse(this.options.UserAgentQueue), serviceEndpoint);
        this.log = log;
    }

    public async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        var pageInfo = await this.dispatcher.RunAsync(new PageInfoQuery(new(url)) { CancellationToken = cancellationToken });

        //if (!string.IsNullOrEmpty(pageInfo.Source))
        //{
        //    await this.dispatcher.ExecuteAsync(new GoBlankCommand());
        //}

        return pageInfo.Source;
    }
}

public record PageInfo
{
    public PageInfo(Uri uri)
    {
        this.Uri = uri;
    }

    public Uri Uri { get; init; }
    public string? Source { get; init; }
}

public class PageInfoQuery : Query<PageInfo>
{
    public PageInfoQuery(Uri pageUri)
    {
        this.PageUri = pageUri;
    }

    public Uri PageUri { get; }
    public string? Script { get; init; }
}

public class GoBlankCommand : Command
{
}

