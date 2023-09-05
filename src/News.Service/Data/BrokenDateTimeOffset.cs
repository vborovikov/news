namespace News.Service.Data;

using System;
using System.Diagnostics;
using System.Globalization;

static class BrokenDateTimeOffset
{
    // they better should provide correct offsets instead of abbreviations, we just assume the values here
    private static readonly Dictionary<string, string> knownTimeZones = new()
    {
        { "CST", "-06:00" },
        { "EDT", "-04:00" },
        { "EST", "-05:00" },
        { "GMT", "+00:00" },
        { "MDT", "-06:00" },
        { "MST", "-07:00" },
        { "PDT", "-07:00" },
        { "PST", "-08:00" },
        { "UT",  "+00:00" },
        { "UTC", "+00:00" },
    };

    public static bool TryParse(ReadOnlySpan<char> str, out DateTimeOffset dateTimeOffset)
    {
        Span<char> buffer = stackalloc char[31];
        buffer.Fill(' ');

        var components = new DateTimeOffsetEnumerator(str);
        while (components.MoveNext())
        {
            var cmp = components.Current;
            switch (cmp.Type)
            {
                case DateTimeOffsetComponentType.DayOfWeek:
                    cmp.Span[..3].CopyTo(buffer);
                    break;
                case DateTimeOffsetComponentType.Day:
                    if (cmp.Span.Length < 2)
                    {
                        buffer[4] = '0';
                        buffer[5] = cmp.Span[0];
                    }
                    else
                    {
                        cmp.Span.CopyTo(buffer[4..]);
                    }
                    break;
                case DateTimeOffsetComponentType.Month:
                    cmp.Span[..3].CopyTo(buffer[7..]);
                    break;
                case DateTimeOffsetComponentType.Year:
                    cmp.Span.CopyTo(buffer[11..]);
                    break;
                case DateTimeOffsetComponentType.Time:
                    cmp.Span.CopyTo(buffer[16..]);
                    break;
                case DateTimeOffsetComponentType.Offset:
                    cmp.Span.CopyTo(buffer[25..]);
                    break;
                case DateTimeOffsetComponentType.TimeZone:
                    var timeZone = cmp.Span[..Math.Min(3, cmp.Span.Length)];
                    foreach (var tz in knownTimeZones)
                    {
                        if (timeZone.Equals(tz.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            tz.Value.CopyTo(buffer[25..]);
                            break;
                        }
                    }
                    break;
            }
        }

        // "ddd dd MMM yyyy HH:mm:ss K" but we remove day-of-week component
        return DateTimeOffset.TryParse(buffer[4..], CultureInfo.InvariantCulture,
            DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite | DateTimeStyles.AssumeUniversal,
            out dateTimeOffset);
    }

    private enum DateTimeOffsetComponentType
    {
        Unknown,
        DayOfWeek,
        Day,
        Month,
        Year,
        Time,
        Offset,
        TimeZone
    }

    [DebuggerDisplay("{Span}: {Type}")]
    private readonly ref struct DateTimeOffsetComponent
    {
        public DateTimeOffsetComponent(ReadOnlySpan<char> span, DateTimeOffsetComponentType type)
        {
            this.Span = span;
            this.Type = type;
        }

        public ReadOnlySpan<char> Span { get; }
        public DateTimeOffsetComponentType Type { get; }
    }

    private ref struct DateTimeOffsetEnumerator
    {
        private ReadOnlySpan<char> span;
        private DateTimeOffsetComponent current;
        private DateTimeOffsetComponentType type;

        public DateTimeOffsetEnumerator(ReadOnlySpan<char> span)
        {
            this.span = span;
            this.current = default;
            this.type = DateTimeOffsetComponentType.Unknown;
        }

        public readonly DateTimeOffsetComponent Current => this.current;

        public readonly DateTimeOffsetEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            var remaining = this.span;
            if (remaining.IsEmpty)
                return false;

            var start = remaining.IndexOfAnyExcept(' ');
            if (start >= 0)
            {
                ++this.type;

                remaining = remaining[start..];
                var end = remaining.IndexOf(' ');
                if (end > 0)
                {
                    var component = remaining[..end];

                    this.type = SpecifyType(component, this.type);
                    if (this.type == DateTimeOffsetComponentType.Unknown)
                        goto UnknownComponent;
                    
                    this.current = new(component, this.type);
                    this.span = remaining[(end + 1)..];
                    return true;
                }

                this.type = SpecifyType(remaining, this.type);
                if (this.type == DateTimeOffsetComponentType.Unknown)
                    goto UnknownComponent;
                
                this.current = new(remaining, this.type);
                this.span = default;
                return true;
            }

        UnknownComponent:
            this.span = default;
            return false;
        }

        private static DateTimeOffsetComponentType SpecifyType(ReadOnlySpan<char> span, DateTimeOffsetComponentType suggestedType)
        {
            return
                char.IsAsciiDigit(span[0]) ? span switch
                {
                    { Length: <= 2 } => DateTimeOffsetComponentType.Day,

                    { Length: 4 } => DateTimeOffsetComponentType.Year,

                    { Length: 8 } => DateTimeOffsetComponentType.Time,

                    _ => suggestedType,
                } :
                char.IsAsciiLetter(span[0]) ? span switch
                {
                    { Length: 2 } => DateTimeOffsetComponentType.TimeZone,

                    { Length: 3 } =>
                        char.IsAsciiLetterUpper(span[0]) &&
                        char.IsAsciiLetterUpper(span[1]) &&
                        char.IsAsciiLetterUpper(span[2]) ? DateTimeOffsetComponentType.TimeZone :
                        suggestedType == DateTimeOffsetComponentType.Day ? DateTimeOffsetComponentType.Month :
                        suggestedType,

                    { Length: > 3 } =>
                        char.IsAsciiLetterUpper(span[0]) &&
                        char.IsAsciiLetterUpper(span[1]) &&
                        char.IsAsciiLetterUpper(span[2]) ? DateTimeOffsetComponentType.TimeZone :
                        span[^1] == ',' ? DateTimeOffsetComponentType.DayOfWeek :
                        DateTimeOffsetComponentType.Month,

                    _ => suggestedType,
                } :
                span[0] == '+' || span[0] == '-' ? DateTimeOffsetComponentType.Offset :
                DateTimeOffsetComponentType.Unknown;
        }
    }
}
