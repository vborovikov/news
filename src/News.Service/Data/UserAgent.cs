namespace News.Service.Data;

using System;
using System.Net;
using System.Threading.Tasks;
using Dodkin.Dispatch;
using Storefront.UserAgent;

static class UserAgent
{
    private static readonly TimeSpan timeout = TimeSpan.FromMinutes(5);

    public static async Task<string?> GetStringAsync(this IQueueRequestDispatcher dispatcher, string url, CancellationToken cancellationToken)
    {
        var pageInfo = await dispatcher.RunAsync(new PageInfoQuery(new Uri(url)) { CancellationToken = cancellationToken }, timeout);

        if (pageInfo is { StatusCode: >= 300 })
        {
            var httpStatucCode = (HttpStatusCode)pageInfo.StatusCode;
            throw new HttpRequestException(
                $"Response status code does not indicate success: {pageInfo.StatusCode} ({httpStatucCode}).",
                null, httpStatucCode);
        }

        return pageInfo?.Source;
    }
}
