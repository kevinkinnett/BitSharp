using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using BitSharp.BlockHelper;
using System.Linq;
using System.IO;

namespace BitSharp.Metrics
{
    public class Metrics
    {
        public static void Main(string[] args)
        {
            //new MethodTimer().Time(() =>
            //{
            //    TimeBlockEncode();
            //    TimeBlockDecode();

            //    TimeTransactionEncode();
            //    TimeTransactionDecode();
            //});
        }

    //    private static void TimeBlockEncode()
    //    {
    //        var blockProvider = new FileSystemBlockProvider();
    //        var block = blockProvider.GetBlock(200.THOUSAND());

    //        var stopwatch = new Stopwatch();
    //        stopwatch.Start();

    //        var count = 1 * 1000;
    //        Parallel.For(0, count, i => block.With());

    //        stopwatch.Stop();
    //        var rate = ((float)count / ((float)stopwatch.ElapsedMilliseconds / 1000)).ToString("#,##0");
    //        Debug.WriteLine("Block Encode:          {0,12} block/s".Format2(rate));
    //    }

    //    private static void TimeBlockDecode()
    //    {
    //        var blockProvider = new FileSystemBlockProvider();
    //        var block = blockProvider.GetBlock(200.THOUSAND());
    //        var blockBytes = block.ToRawBytes();

    //        var stopwatch = new Stopwatch();
    //        stopwatch.Start();

    //        var count = 1.THOUSAND();
    //        Parallel.For(0, count, i => Block.FromRawBytes(blockBytes));

    //        stopwatch.Stop();
    //        var rate = ((float)count / ((float)stopwatch.ElapsedMilliseconds / 1000)).ToString("#,##0");
    //        Debug.WriteLine("Block Decode:          {0,12} block/s".Format2(rate));
    //    }

    //    private static void TimeTransactionEncode()
    //    {
    //        var blockProvider = new FileSystemBlockProvider();
    //        var block = blockProvider.GetBlock(200000);

    //        var stopwatch = new Stopwatch();
    //        stopwatch.Start();

    //        var count = 1.MILLION();
    //        Parallel.For(0, count, i => block.Transactions.RandomOrDefault().With());

    //        stopwatch.Stop();
    //        var rate = ((float)count / ((float)stopwatch.ElapsedMilliseconds / 1000)).ToString("#,##0");
    //        Debug.WriteLine("Transaction Encode:    {0,12} tx/s".Format2(rate));
    //    }

    //    private static void TimeTransactionDecode()
    //    {
    //        var blockProvider = new FileSystemBlockProvider();
    //        var block = blockProvider.GetBlock(200000);
    //        var txBytesList = block.Transactions.Select(x => x.ToRawBytes()).ToList();

    //        var stopwatch = new Stopwatch();
    //        stopwatch.Start();

    //        var count = 1.MILLION();
    //        Parallel.For(0, count, i => Transaction.FromRawBytes(txBytesList.RandomOrDefault()));

    //        stopwatch.Stop();
    //        var rate = ((float)count / ((float)stopwatch.ElapsedMilliseconds / 1000)).ToString("#,##0");
    //        Debug.WriteLine("Transaction Decode:    {0,12} tx/s".Format2(rate));
    //    }
    }
}
