namespace News.Service.Data;

using System.Buffers;
using System.Diagnostics;
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

    public static string ToText(this Document document) =>
        new StringBuilder(document.Length)
        .AppendElements(document.GetEnumerator())
        .ToString();

    public static string ToText(this ParentTag root) =>
        new StringBuilder(root.Length)
        .AppendElements(root.GetEnumerator())
        .ToString();

    private static StringBuilder AppendElements(this StringBuilder text, Element.Enumerator elements)
    {
        foreach (var element in elements)
        {
            text.AppendElement(element);
        }

        return text;
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

    private const string trimChars = "@!(),-.:;?";
    private static readonly SearchValues<char> escapeChars = SearchValues.Create("<>\"'&");
    private static readonly string[] escapeStringPairs =
    [
        // these must be all once character escape sequences or a new escaping algorithm is needed
        "<", "&lt;",
        ">", "&gt;",
        "\"", "&quot;",
        "\'", "&apos;",
        "&", "&amp;"
    ];

    private static ReadOnlySpan<char> Escape(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
            return [];

        StringBuilder? sb = null;

        int pos;
        while ((pos = span.IndexOfAny(escapeChars)) >= 0)
        {
            sb ??= new StringBuilder();
            sb.Append(span[..pos]).Append(GetEscapeSequence(span[pos]));
            span = span[(pos + 1)..];
        }

        return sb == null ? span : sb.Append(span).ToString();
    }

    private static string GetEscapeSequence(char c)
    {
        var iMax = escapeStringPairs.Length;
        Debug.Assert(iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly");

        for (int i = 0; i < iMax; i += 2)
        {
            string strEscSeq = escapeStringPairs[i];
            string strEscValue = escapeStringPairs[i + 1];

            if (strEscSeq[0] == c)
                return strEscValue;
        }

        Debug.Fail("Unable to find escape sequence for this character");
        return c.ToString();
    }
}
