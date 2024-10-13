namespace News.App.Pages.Shared;

using System.Globalization;
using Microsoft.AspNetCore.Mvc.Razor;
using News.App.Data;
using Relay.InteractionModel;

static class PageExtensions
{
    private const string DefaultPageSizeCookieName = "pageSize";
    private static readonly TimeSpan PageSizeCookieMaxAge = TimeSpan.FromDays(180);
    private const string PageNumberParamName = nameof(PageRequest.P);
    private const string PageSizeParamName = nameof(PageRequest.Ps);

    public static (Dictionary<string, string> RouteData, int PageNumber, int PageSize)
        GetPageQueryParams(this RazorPage page, string pageSizeCookieNameSuffix = DefaultPageSizeCookieName,
            Func<int?, int>? normalizePageSize = null)
    {
        var routeData = page.Context.Request.Query
            .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var p = Page.FirstPageNumber;
        if (routeData.TryGetValue(PageNumberParamName, out var pval) && int.TryParse(pval, out var pn))
        {
            p = pn;
            routeData.Remove(PageNumberParamName);
        }

        var pageSizeCookieName = GetPageSizeCookieName(page.Context, pageSizeCookieNameSuffix);
        var normalize = normalizePageSize ?? Page.NormalizePageSize;
        var ps = page.Context.Request.Cookies.TryGetValue(pageSizeCookieName, out var pageSizeCookie) &&
            int.TryParse(pageSizeCookie, CultureInfo.InvariantCulture, out var pageSize) ?
            normalize(pageSize) : normalize(default);

        if (routeData.TryGetValue(PageSizeParamName, out var psval) && int.TryParse(psval, out var psn))
        {
            var requestedPageSize = normalize(psn);
            var shouldStore = ps != requestedPageSize;

            ps = normalize(psn);
            routeData.Remove(PageSizeParamName);

            if (shouldStore)
            {
                page.Context.Response.Cookies.Append(pageSizeCookieName, psval,
                    new CookieOptions { MaxAge = PageSizeCookieMaxAge });
            }
        }

        return (routeData, p, ps);
    }

    public static int GetPageSize(this PageRequest pageRequest, HttpContext context, string pageSizeCookieNameSuffix = DefaultPageSizeCookieName)
    {
        var pageSizeCookieName = GetPageSizeCookieName(context, pageSizeCookieNameSuffix);

        var ps = pageRequest.Ps ?? (context.Request.Cookies.TryGetValue(pageSizeCookieName, out var pageSizeCookie) &&
            int.TryParse(pageSizeCookie, CultureInfo.InvariantCulture, out var pageSize) ? pageSize : null);

        return pageRequest is ChannelPageRequest ? ChannelPageRequest.NormalizePageSize(ps) : Page.NormalizePageSize(ps);
    }

    private static string GetPageSizeCookieName(HttpContext context, string pageSizeCookieNameSuffix)
    {
        var pageSizeCookieNamePrefix = string.Concat(
            context.Request.RouteValues["channel"] as string,
            ToTitleCase(context.Request.RouteValues["feed"] as string));

        var pageSizeCookieName = string.IsNullOrWhiteSpace(pageSizeCookieNamePrefix) ? pageSizeCookieNameSuffix :
            string.Concat(pageSizeCookieNamePrefix, ToTitleCase(pageSizeCookieNameSuffix));

        return pageSizeCookieName;
    }

    private static string ToTitleCase(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name ?? string.Empty;

        return string.Create(name.Length, name, (span, src) =>
        {
            src.CopyTo(span);
            span[0] = char.ToUpperInvariant(name[0]);
        });
    }
}
