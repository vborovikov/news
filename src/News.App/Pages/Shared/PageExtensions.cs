namespace News.App.Pages.Shared;

using Microsoft.AspNetCore.Mvc.Razor;
using Relay.InteractionModel;

static class PageExtensions
{
    public static (Dictionary<string, string> RouteData, int PageNumber, int PageSize) GetPageQueryParams(this RazorPage page)
    {
        var routeData = page.Context.Request.Query.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value.ToString());

        var p = Page.FirstPageNumber;
        if (routeData.TryGetValue("p", out var pval) && int.TryParse(pval, out var pn))
        {
            p = pn;
            routeData.Remove("p");
        }
        
        var ps = Page.AvailablePageSizes[0];
        if (routeData.TryGetValue("ps", out var psval) && int.TryParse(psval, out var psn))
        {
            ps = psn;
            routeData.Remove("ps");
        }

        return (routeData, p, ps);
    }
}
