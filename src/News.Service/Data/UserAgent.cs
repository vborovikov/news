namespace News.Service.Data;

using System;
using System.Net;
using System.Threading.Tasks;
using Dodkin;
using Dodkin.Dispatch;
using Microsoft.Extensions.Options;

sealed class UserAgent
{
    private static readonly MessageEndpoint serviceEndpoint = MessageEndpoint.FromName(ServiceOptions.ServiceName.ToLowerInvariant());
    private static readonly TimeSpan timeout = TimeSpan.FromMinutes(5);

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
        this.log = log;
        try
        {
            this.dispatcher = new QueueRequestDispatcher(MessageQueueName.Parse(this.options.UserAgentQueue), serviceEndpoint);
        }
        catch (Exception x)
        {
            this.log.LogError(x, "Error creating request dispatcher for message queue {queueName}", this.options.UserAgentQueue);
            throw;
        }
    }

    public async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        var pageInfo = await this.dispatcher.RunAsync(new PageInfoQuery(new(url)) { CancellationToken = cancellationToken }, timeout);
        await this.dispatcher.ExecuteAsync(new GoBlankCommand());

        if (pageInfo is not null && pageInfo.StatusCode >= 300)
        {
            var httpStatucCode = (HttpStatusCode)pageInfo.StatusCode;
            throw new HttpRequestException(
                $"Response status code does not indicate success: {pageInfo.StatusCode} ({httpStatucCode}).",
                null, httpStatucCode);
        }

        return pageInfo?.Source;
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
    public int StatusCode { get; init; }
}

public class PageInfoQuery : Query<PageInfo>
{
    public PageInfoQuery(Uri pageUri)
    {
        this.PageUri = pageUri;
    }

    public Uri PageUri { get; }
    public string? Script { get; init; }
    public bool UseContent => true;
}

public class GoBlankCommand : Command
{
}

