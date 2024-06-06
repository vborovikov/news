namespace News.App.Pages.Shared;

using System.Globalization;
using Microsoft.AspNetCore.Mvc.Razor;
using Relay.InteractionModel;

static class PageExtensions
{
    private const string DefaultPageSizeCookieName = "pageSize";

    public static (Dictionary<string, string> RouteData, int PageNumber, int PageSize) 
        GetPageQueryParams(this RazorPage page, string pageSizeCookieName = DefaultPageSizeCookieName)
    {
        var routeData = page.Context.Request.Query.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value.ToString());

        var p = Page.FirstPageNumber;
        if (routeData.TryGetValue("p", out var pval) && int.TryParse(pval, out var pn))
        {
            p = pn;
            routeData.Remove("p");
        }
        
        var ps = page.Context.Request.Cookies.TryGetValue(pageSizeCookieName, out var pageSizeCookie) &&
            int.TryParse(pageSizeCookie, CultureInfo.InvariantCulture, out var pageSize) ? 
            Page.NormalizePageSize(pageSize) : Page.AvailablePageSizes[0];

        if (routeData.TryGetValue("ps", out var psval) && int.TryParse(psval, out var psn))
        {
            ps = Page.NormalizePageSize(psn);
            routeData.Remove("ps");
            page.Context.Response.Cookies.Append(pageSizeCookieName, ps.ToString(CultureInfo.InvariantCulture));
        }

        return (routeData, p, ps);
    }

    public static int GetPageSize(this PageRequest pageRequest, HttpContext context, string pageSizeCookieName = DefaultPageSizeCookieName)
    {
        var ps = pageRequest.Ps ?? (context.Request.Cookies.TryGetValue(pageSizeCookieName, out var pageSizeCookie) &&
            int.TryParse(pageSizeCookie, CultureInfo.InvariantCulture, out var pageSize) ? pageSize : null);

        return Page.NormalizePageSize(ps);
    }
}
