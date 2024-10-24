namespace News.App.Data;

using Microsoft.AspNetCore.Mvc.Rendering;
using Spryer;

static class HtmlHelperExtensions
{
    public static IEnumerable<SelectListItem> GetDbEnumSelectList<TEnum>(this IHtmlHelper html)
         where TEnum : struct, Enum
    {
        var selectList = html.GetEnumSelectList<TEnum>();

        // DbEnum conversion to string produces a name not a value
        // and so the AspNetCore infra doesn't recognize the selected value
        var names = DbEnum<TEnum>.GetNames();
        var values = DbEnum<TEnum>.GetValues().Select(val => val.ToString("d")).ToArray();
        foreach (var item in selectList)
        {
            for (var i = 0; i < values.Length; ++i)
            {
                if (item.Value.Equals(values[i], StringComparison.Ordinal))
                {
                    item.Value = names[i];
                }
            }
        }

        return selectList;
    }
}
