﻿namespace News
{
    [Flags]
    public enum FeedSafeguard
    {
        // Does nothing to the feed posts
        None = 0 << 0,
        OK = None,

        // Downloads content from the post link
        ContentExtractor = 1 << 0,
        CODE = ContentExtractor,

        // Trims last paragraph or inline content in the description and the content
        LastParaTrimmer = 1 << 1,
        PARA = LastParaTrimmer,

        // Replaces the description with the content
        DescriptionReplacer = 1 << 2,
        DESC = DescriptionReplacer,

        // Removes description images
        DescriptionImageRemover = 1 << 3,
        DIMG = DescriptionImageRemover,

        // Downloads image files and replaces image links with local file links
        ImageLinkFixer = 1 << 4,
        FIMG = ImageLinkFixer,

        // Replaces external links to posts with internal links if possible
        PostLinkFixer = 1 << 5,
        FPST = PostLinkFixer,
    }
}