using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;

namespace BitSharp.Common
{
    public static class LookAheadMethods
    {
        public static IEnumerable<T> LookAhead<T>(Func<IEnumerable<T>> values, CancellationToken cancelToken)
        {
            // setup task completion sources to read results of look ahead
            using (var resultWriteEvent = new AutoResetEvent(false))
            using (var resultReadEvent = new AutoResetEvent(false))
            using (var internalCancelToken = new CancellationTokenSource())
            {
                var results = new ConcurrentDictionary<int, T>();
                var resultReadIndex = new[] { -1 };
                var targetIndex = new[] { 0 };

                var finalCount = -1;

                var readTimes = new List<DateTime>();
                readTimes.Add(DateTime.UtcNow);

                var lookAheadThread = new Thread(() =>
                {
                    // look-ahead loop
                    var indexLocal = 0;
                    var valuesLocal = values();
                    foreach (var value in valuesLocal)
                    {
                        // cooperative loop
                        if (internalCancelToken.IsCancellationRequested || (cancelToken != null && cancelToken.IsCancellationRequested))
                        {
                            return;
                        }

                        // store the result and notify
                        results.TryAdd(indexLocal, value);
                        resultWriteEvent.Set();

                        // make sure look-ahead doesn't go too far ahead, based on calculated index above
                        while (indexLocal > targetIndex[0])
                        {
                            // cooperative loop
                            if (internalCancelToken.IsCancellationRequested || (cancelToken != null && cancelToken.IsCancellationRequested))
                            {
                                return;
                            }

                            // wait for a result to be read
                            resultReadEvent.WaitOne(TimeSpan.FromMilliseconds(10));
                        }

                        indexLocal++;
                    }

                    // notify done
                    finalCount = results.Count;
                    resultWriteEvent.Set();
                });

                lookAheadThread.Start();
                try
                {
                    // enumerate the results
                    var i = 0;
                    while (finalCount == -1 || i < finalCount)
                    {
                        // cooperative loop
                        if (cancelToken != null)
                            cancelToken.ThrowIfCancellationRequested();

                        // unblock loook-ahead and wait for current result to be come available
                        resultReadEvent.Set();
                        while (i >= results.Count && (finalCount == -1 || i < finalCount))
                        {
                            // cooperative loop
                            if (cancelToken != null)
                                cancelToken.ThrowIfCancellationRequested();

                            resultWriteEvent.WaitOne(TimeSpan.FromMilliseconds(10));
                        }

                        // check if enumration is finished
                        if (finalCount != -1 && i >= finalCount)
                            break;

                        // retrieve current result and clear reference
                        T result = results[i];
                        results[i] = default(T);

                        // update current index and unblock look-ahead
                        resultReadIndex[0] = i;
                        resultReadEvent.Set();
                        i++;

                        // yield result
                        yield return result;

                        // store time the result was read
                        readTimes.Add(DateTime.UtcNow);
                        while (readTimes.Count > 500)
                            readTimes.RemoveAt(0);

                        // calculate how far to look-ahead based on how quickly the results are being read
                        var firstReadTime = readTimes[0];
                        var readPerMillisecond = (float)(readTimes.Count / (DateTime.UtcNow - firstReadTime).TotalMilliseconds);
                        targetIndex[0] = resultReadIndex[0] + 1 + (int)(readPerMillisecond * 1000); // look ahead 1000 milliseconds
                    }
                }
                finally
                {
                    // ensure look-ahead thread is cleaned up
                    internalCancelToken.Cancel();
                    lookAheadThread.Join();
                }
            }
        }
    }
}