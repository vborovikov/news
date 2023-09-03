namespace News.Tests.Service;

[TestClass]
public class SlugTests
{
    [DataTestMethod]
    [DataRow("http://example.com/?p=474", "474")]
    [DataRow("http://example.com/Images-SI-Uranium-Ore/dp/B000796XXM/ref=sr_1_9?ie=UTF8&qid=1403704779&sr=8-9&keywords=uranium+ore", "B000796XXM")]
    [DataRow("http://example.com/2010/11/in-event-of-moon-disaster.html?m=1", "in-event-of-moon-disaster")]
    [DataRow("http://example.com/feeds/5775337322498187563/comments/default", "5775337322498187563")]
    [DataRow("http://example.com/article/2039629/microsoft-turns-siri-against-apple-in-hilarious-new-windows-8-ad.html?%23tk.out_mod?=obinsite", "microsoft-turns-siri-against-apple-in-hilarious-new-windows-8-ad")]
    [DataRow("https://example.com/hillelwayne/archive/i-have-complicated-feelings-about-tdd-8403/#fn:powershell", "i-have-complicated-feelings-about-tdd-8403")]
    [DataRow("https://example.com/#2021-02-03", "2021-02-03")]
    [DataRow("https://example.com/2014/01/26/the-beckoning/", "the-beckoning")]
    [DataRow("https://example.com/log/#019", "019")]
    [DataRow("http://example.com/post/74792148262/the-iphone-company#fnref:p74792148262-2", "the-iphone-company")]
    [DataRow("https://example.com/questions/13466/can-grep-output-only-specified-groupings-that-match#13472", "can-grep-output-only-specified-groupings-that-match")]
    [DataRow("https://example.com/2023/06/20/money-buys-happiness/?utm_source=rss&utm_medium=rss&utm_campaign=money-buys-happiness", "money-buys-happiness")]
    [DataRow("https://example.com/blog/?p=69", "69")]
    [DataRow("https://www.infoq.com/news/2023/09/java-21-so-far/?utm_campaign=infoq_content&utm_source=infoq&utm_medium=feed&utm_term=global", "java-21-so-far")]
    public void SlugifyPost_NormalLinks_LastPathAsSlug(string link, string expected)
    {
        var slug = link.SlugifyPost();
        Assert.AreEqual(expected, slug);
    }

    [DataTestMethod]
    [DataRow("https://example.com/2020/04/03/zooms-encryption-is-not-suited-for-secrets-and-has-surprising-links-to-china-researchers-discover/)", "zooms-encryption-is-not-suited-for-secrets-and-has-surprising-links-to-china-researchers-discover")]
    [DataRow("http://example.com/blog/2023/06/15/.FOIA-Request-Saga-For-Computer-Related-Things", ".FOIA-Request-Saga-For-Computer-Related-Things")]
    [DataRow("https://example.com/article/2023/02/.net-serialization-benchmarks-feb-2023/", ".net-serialization-benchmarks-feb-2023")]
    [DataRow("http://example.com/blog/-analogs-stranglehold-of-the-classroom", "analogs-stranglehold-of-the-classroom")]
    [DataRow("https://example.com/--version-always.html", "version-always")]
    [DataRow("/audio-books/", "audio-books")]
    [DataRow("https://example.com/blog/2023/08/04/blogging-all-the-blogs/", "blogging-all-the-blogs")]
    [DataRow("https://example.com/04.effect-concurrency", "04.effect-concurrency")]
    [DataRow("https://example.com/books/awesome-lists/00.specials/weekly/2018/1/1.4-en/", "1.4-en")]
    [DataRow("https://example.com/books/awesome-lists/00.specials/weekly/2017/11/11.3/", "11.3")]
    [DataRow("https://example.com/daypages/2016.11.05/", "2016.11.05")]
    [DataRow("https://example.com/journal/2023/newsletter-rule-in-quiet/", "newsletter-rule-in-quiet")]
    [DataRow("https://example.com/journal/2023/comments-rule-in-quiet/", "comments-rule-in-quiet")]
    [DataRow("https://example.com/blogging/posts/blogtober-2016-1-2/", "blogtober-2016-1-2")]
    public void SlugifyPost_WeirdLinks_LastGoodPathAsSlug(string link, string expected)
    {
        var slug = link.SlugifyPost();
        Assert.AreEqual(expected, slug);
    }

    [DataTestMethod]
    [DataRow("https://blog.antosubash.com/rss.xml", "antosubash")]
    [DataRow("https://chrissainty.com/rss/", "chrissainty")]
    [DataRow("https://www.respawnsive.com/en/feed/", "respawnsive")]
    [DataRow("https://jameseastham.co.uk/index.xml", "jameseastham")]
    [DataRow("https://www.jvt.me/feed.xml", "jvt")]
    [DataRow("https://erikej.github.io/feed.xml", "erikej")]
    [DataRow("https://webjose.hashnode.dev/rss.xml", "webjose")]
    [DataRow("https://jonahandersson.tech/feed/", "jonahandersson")]
    [DataRow("https://www.milanjovanovic.tech/rss/feed.xml", "milanjovanovic")]
    public void SlugifyFeed_NormalLinks_DomainAsSlug(string link, string expected)
    {
        var slug = link.SlugifyFeed();
        Assert.AreEqual(expected, slug);
    }
}