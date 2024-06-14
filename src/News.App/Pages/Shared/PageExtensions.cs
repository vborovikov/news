namespace News.App.Pages.Shared;

using System.Globalization;
using Microsoft.AspNetCore.Mvc.Razor;
using News.App.Data;
using Relay.InteractionModel;

static class PageExtensions
{
    private const string DefaultPageSizeCookieName = "pageSize";

    public static (Dictionary<string, string> RouteData, int PageNumber, int PageSize) 
        GetPageQueryParams(this RazorPage page, string pageSizeCookieName = DefaultPageSizeCookieName,
            Func<int?, int>? normalizePageSize =  null)
    {
        var routeData = page.Context.Request.Query.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value.ToString());

        var p = Page.FirstPageNumber;
        if (routeData.TryGetValue("p", out var pval) && int.TryParse(pval, out var pn))
        {
            p = pn;
            routeData.Remove("p");
        }

        var normalize = normalizePageSize ?? Page.NormalizePageSize;
        var ps = page.Context.Request.Cookies.TryGetValue(pageSizeCookieName, out var pageSizeCookie) &&
            int.TryParse(pageSizeCookie, CultureInfo.InvariantCulture, out var pageSize) ? 
            normalize(pageSize) : normalize(default);

        if (routeData.TryGetValue("ps", out var psval) && int.TryParse(psval, out var psn))
        {
            var requestedPageSize = normalize(psn);
            var shouldStore = ps != requestedPageSize;

            ps = normalize(psn);
            routeData.Remove("ps");

            if (shouldStore)
            {
                page.Context.Response.Cookies.Append(pageSizeCookieName, psval,
                    new CookieOptions { Expires = DateTimeOffset.Now.AddMonths(1) });
            }
        }

        return (routeData, p, ps);
    }

    public static int GetPageSize(this PageRequest pageRequest, HttpContext context, string? pageSizeCookieName = null)
    {
        var channel = pageRequest is ChannelPageRequest;
        var cookieName = pageSizeCookieName ?? (channel ? ChannelPageRequest.PageSizeCookieName : DefaultPageSizeCookieName);

        var ps = pageRequest.Ps ?? (context.Request.Cookies.TryGetValue(cookieName, out var pageSizeCookie) &&
            int.TryParse(pageSizeCookie, CultureInfo.InvariantCulture, out var pageSize) ? pageSize : null);

        return channel ? ChannelPageRequest.NormalizePageSize(ps) : Page.NormalizePageSize(ps);
    }
}
