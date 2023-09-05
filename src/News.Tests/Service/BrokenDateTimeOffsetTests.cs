namespace News.Tests.Service;

using System;

[TestClass]
public class BrokenDateTimeOffsetTests
{
    [DataTestMethod]
    [DataRow("01 Sep 2023 08:00:00 EST", "2023-09-01T08:00:00", "-05:00")]
    [DataRow("Sun, 05 Mar 2023 17:30:00 EST", "2023-03-05T17:30:00", "-05:00")]
    [DataRow("Wed, 02 Feb 2022 14:22:00 -0500", "2022-02-02T14:22:00", "-05:00")]
    [DataRow("Tue Oct 08 2019 00:00:00 GMT+0000 (Coordinated Universal Time)", "2019-10-08T00:00:00", "00:00")]
    [DataRow("Thu, 23 Sept 2021, 09:07:29 GMT", "2021-09-23T09:07:29", "00:00")]
    [DataRow("Thu Dec  6 23:40:09 MST 2018", "2018-12-06T23:40:09", "-07:00")]
    [DataRow("Tue Feb 14 23:40:09 MST 2017", "2017-02-14T23:40:09", "-07:00")]
    [DataRow("Tue Oct  2 23:40:09 MDT 2018", "2018-10-02T23:40:09", "-06:00")]
    [DataRow("Wed, 14 Jun 2023 00:00:00 UT", "2023-06-14T00:00:00", "00:00")]
    [DataRow("Mon, 18 August 2023 02:00:00 +0000", "2023-08-18T02:00:00", "00:00")]
    public void TryParse_BrokenDates_ParsedAsCorrectDTO(string dateTimeOffset, string dateTime, string offset)
    {
        Assert.IsTrue(BrokenDateTimeOffset.TryParse(dateTimeOffset, out var parsed));

        Assert.AreEqual(DateTime.Parse(dateTime), parsed.DateTime);
        Assert.AreEqual(TimeSpan.Parse(offset), parsed.Offset);
    }

    [DataTestMethod]
    [DataRow("Tue, 01 Aug 2023 11:23:32 +1600", "2023-08-02T03:23:32", "00:00")]
    [DataRow("Mon, 03 Apr 2023 17:10:00 +2000", "2023-04-04T13:10:00", "00:00")]
    public void TryParse_ImpossibleOffset_DateTimeOffsetAdjusted(string dateTimeOffset, string dateTime, string offset)
    {
        Assert.IsTrue(BrokenDateTimeOffset.TryParse(dateTimeOffset, out var parsed));

        Assert.AreEqual(DateTime.Parse(dateTime), parsed.DateTime);
        Assert.AreEqual(TimeSpan.Parse(offset), parsed.Offset);
    }

}
