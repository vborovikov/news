namespace News.Tests.Service;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

}
