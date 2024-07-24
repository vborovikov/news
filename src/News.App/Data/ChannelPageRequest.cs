namespace News.App.Data;

using System;
using System.Linq;
using Relay.InteractionModel;

sealed class ChannelPageRequest : PageRequest
{
    public static readonly int[] PageSizes = [3, 5, 10];

    public ChannelPageRequest(PageRequest page) : base(page)
    {
        // use original ps value
        this.Ps = page.Ps;
    }

    protected override int NormalizePageSizeOverride(int? pageSize) => NormalizePageSize(pageSize);

    public static int NormalizePageSize(int? pageSize)
    {
        if (pageSize == null)
            return PageSizes[0];

        return PageSizes.Aggregate((x, y) => Math.Abs(x - pageSize.Value) < Math.Abs(y - pageSize.Value) ? x : y);
    }
}
