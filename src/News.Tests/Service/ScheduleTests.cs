namespace News.Tests.Service;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ScheduleTests
{
    [TestMethod]
    public void DateCompare_NullDate_NotNullIsGreater()
    {
        var date = DateTime.Now;
        var nullDate = default(DateTime?);

        var result = nullDate is null || date > nullDate;

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void UpdateInteval_EmptyList_NullValue()
    {
        var pubDates = Array.Empty<DateTimeOffset?>();
        var avgPeriodInSeconds = pubDates.Zip(pubDates.Skip(1), (newer, older) => newer - older).Average(t => t?.TotalSeconds);

        Assert.IsFalse(avgPeriodInSeconds.HasValue);
    }
}
