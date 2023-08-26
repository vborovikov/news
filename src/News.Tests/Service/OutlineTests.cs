namespace News.Tests.Service;

[TestClass]
public class OutlineTests
{
    [DataTestMethod]
    [DataRow("Anto Subash", "https://blog.antosubash.com/rss.xml", "antosubash")]
    [DataRow("Chris Sainty", "https://chrissainty.com/rss/", "chrissainty")]
    [DataRow("Jérémy BRUN-PICARD", "https://www.respawnsive.com/en/feed/", "respawnsive")]
    [DataRow("James Eastham", "https://jameseastham.co.uk/index.xml", "jameseastham")]
    [DataRow("jamietanna", "https://www.jvt.me/feed.xml", "jvt")]
    [DataRow("jamietanna", "jvt.me/feed.xml", "jvt")]
    [DataRow("Erik Ejlskov Jensen", "https://erikej.github.io/feed.xml", "erikej")]
    [DataRow("José Pablo Ramírez Vargas", "https://webjose.hashnode.dev/rss.xml", "webjose")]
    [DataRow("Jonah Andersson", "https://jonahandersson.tech/feed/", "jonahandersson")]
    [DataRow("Milan Jovanović", "https://www.milanjovanovic.tech/rss/feed.xml", "milanjovanovic")]
    public void Slugify_TopLevelRssXml_DomainAsSlug(string title, string xmlUrl, string slug)
    {
        var feed = new FeedOutline("", title, xmlUrl, null);
        Assert.AreEqual(slug, feed.Text);
    }
}