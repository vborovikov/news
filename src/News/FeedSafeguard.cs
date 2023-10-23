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

        DescriptionReplacer = 1 << 2,
        DESC = DescriptionReplacer,

        DescriptionImageRemover = 1 << 3,
        DIMG = DescriptionImageRemover,
    }
}