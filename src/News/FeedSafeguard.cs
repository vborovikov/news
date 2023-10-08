namespace News
{
    [Flags]
    public enum FeedSafeguard
    {
        None = 0 << 0,
        OK = None,

        CodeBlockEncoder = 1 << 0,
        CODE = CodeBlockEncoder,

        LastParaTrimmer = 1 << 1,
        PARA = LastParaTrimmer,

        DescriptionRemover = 1 << 2,
        DESC = DescriptionRemover,

        DescriptionImageRemover = 1 << 3,
        DIMG = DescriptionImageRemover,
    }
}