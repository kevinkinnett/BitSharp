using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common.Test
{
    public abstract class TestBase
    {
        private Stopwatch stopwatch = new Stopwatch();

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void BaseTestInitialize()
        {
            //Debug.WriteLine(string.Format("{0} starting at {1:yyyy-MM-dd HH-mm-ss.fff}", TestContext.TestName, DateTime.Now));
            //Debug.WriteLine(String.Join("", Enumerable.Range(0, 80).Select(x => "-")));
            //Debug.WriteLine("");
            
            stopwatch.Reset();
            stopwatch.Start();
        }

        [TestCleanup]
        public void BaseTestCleanup()
        {
            stopwatch.Stop();
            Debug.WriteLine("");
            Debug.WriteLine(String.Join("", Enumerable.Range(0, 80).Select(x => "-")));
            Debug.WriteLine(string.Format("{0} finished in {1} ms", TestContext.TestName, stopwatch.ElapsedMilliseconds));
        }
    }
}
