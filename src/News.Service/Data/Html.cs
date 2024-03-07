namespace News.Service.Data;

using System.Net;
using System.Text;
using Brackets;

sealed class EncodedContent : Content
{
    private readonly string text;

    public EncodedContent(string text) : base(0, text.Length)
    {
        this.text = WebUtility.HtmlEncode(text);
    }

    protected override ReadOnlySpan<char> Source => this.text;

    public override ReadOnlySpan<char> Data => this.text;

    public override string ToString() => this.text;
}

internal static class HtmlExtensions
{
    public static bool TryDelete(this Element element, bool deleteEmptyAncestors = false)
    {
        var done = false;

        while (element.Parent is ParentTag parent)
        {
            parent.Remove(element);
            done = true;

            if (!deleteEmptyAncestors)
                break;
            if (parent.Any())
                break;
            element = parent;
        }

        return done;
    }

    public static string ToText(this Document document)
    {
        var text = new StringBuilder(document.Length);

        foreach (var element in document)
        {
            text.AppendElement(element);
        }

        return text.ToString();
    }

    public static string ToText(this ParentTag root)
    {
        var text = new StringBuilder(root.Length);

        foreach (var element in root)
        {
            text.AppendElement(element);
        }

        return text.ToString();
    }

    private static void AppendElement(this StringBuilder text, Element element)
    {
        if (element is ParentTag parent)
        {
            text.Append('<').Append(parent.Name);
            if (parent.HasAttributes)
            {
                text.Append(' ').AppendAttributes(parent.EnumerateAttributes());
            }
            text.Append('>');

            foreach (var child in parent)
            {
                text.AppendElement(child);
            }

            text.Append("</").Append(parent.Name).Append('>');
        }
        else
        {
            text.AppendSimpleElement(element);
        }
    }

    private static void AppendSimpleElement(this StringBuilder text, Element element)
    {
        switch (element)
        {
            case Tag tag:
                text.Append('<').Append(tag.Name);
                if (tag.HasAttributes)
                {
                    text.Append(' ').AppendAttributes(tag.EnumerateAttributes()).Append(' ');
                }
                text.Append("/>");
                break;
            case Comment comment:
                text.Append("<!--").Append(comment.Data).Append("-->");
                break;
            case Section section:
                text.Append("<[CDATA[").Append(section.Data).Append("]]>");
                break;
            case Content content:
                text.Append(content.Data);
                break;
            default:
                text.Append(element.ToString());
                break;
        }
    }

    private static StringBuilder AppendAttributes(this StringBuilder text, Attr.Enumerator attributes)
    {
        foreach (var attribute in attributes)
        {
            if (text.Length > 0 && text[^1] != ' ')
            {
                text.Append(' ');
            }
            text.Append(attribute.Name);
            if (attribute.HasValue)
            {
                text
                    .Append('=')
                    .Append('"')
                    .Append(attribute.Value)
                    .Append('"');
            }
        }

        return text;
    }
}
