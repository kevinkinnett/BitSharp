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
        //TODO these methods are lazy with resources.... get IDisposables in order

        public static IEnumerable<TValue> ParallelLookupLookAhead<TKey, TValue>(IList<TKey> keys, Func<TKey, TValue> lookup, CancellationToken cancelToken)
        {
            // setup task completion sources to read results of look ahead
            var results = keys.Select(x => default(TValue)).ToList();
            var resultEvents = keys.Select(x => new ManualResetEvent(false)).ToList();
            var resultReadEvent = new AutoResetEvent(false);
            var resultReadIndex = new[] { -1 };
            var internalCancelToken = new CancellationTokenSource();

            var readTimes = new List<DateTime>();
            readTimes.Add(DateTime.UtcNow);

            var lookAheadThread = new Thread(() =>
            {
                var maxThreads = 5;

                // calculate how far to look-ahead based on how quickly the results are being read
                Func<long> targetIndex = () =>
                {
                    var firstReadTime = readTimes[0];
                    var readPerMillisecond = (float)(readTimes.Count / (DateTime.UtcNow - firstReadTime).TotalMilliseconds);
                    return resultReadIndex[0] + 1 + maxThreads + (int)(readPerMillisecond * 1000); // look ahead 1000 milliseconds
                };

                // look-ahead loop
                Parallel.ForEach(keys, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, (key, loopState, indexLocal) =>
                {
                    // cooperative loop
                    if (internalCancelToken.IsCancellationRequested || (cancelToken != null && cancelToken.IsCancellationRequested))
                    {
                        loopState.Break();
                        return;
                    }

                    // make sure look-ahead doesn't go too far ahead, based on calculated index above
                    while (indexLocal > targetIndex())
                    {
                        // cooperative loop
                        if (internalCancelToken.IsCancellationRequested || (cancelToken != null && cancelToken.IsCancellationRequested))
                        {
                            loopState.Break();
                            return;
                        }

                        // wait for a result to be read
                        resultReadEvent.WaitOne(TimeSpan.FromMilliseconds(10));
                    }

                    // execute the lookup
                    var result = lookup(key);

                    // store the result and notify
                    results[(int)indexLocal] = result;
                    resultEvents[(int)indexLocal].Set();
                });
            });
            lookAheadThread.Start();

            // enumerate the results
            for (var i = 0; i < results.Count; i++)
            {
                TValue result;
                try
                {
                    resultReadEvent.Set();

                    resultEvents[i].WaitOne();

                    result = results[i];
                    results[i] = default(TValue);
                }
                catch (Exception)
                {
                    internalCancelToken.Cancel();
                    lookAheadThread.Join();

                    // clean-up disposables on exception
                    for (var j = i; j < results.Count; j++)
                    {
                        try
                        {
                            resultEvents[j].Dispose();
                        }
                        catch (Exception) { }
                    }

                    // continue with exception
                    throw;
                }

                resultReadIndex[0] = i;
                resultReadEvent.Set();

                // yield result
                yield return result;

                // store time the result was read
                readTimes.Add(DateTime.UtcNow);
                while (readTimes.Count > 500)
                    readTimes.RemoveAt(0);
            }

            internalCancelToken.Cancel();
            lookAheadThread.Join();
        }

        public static IEnumerable<T> LookAhead<T>(Func<IEnumerable<T>> values, CancellationToken cancelToken)
        {
            // setup task completion sources to read results of look ahead
            var resultWriteEvent = new AutoResetEvent(false);
            var resultReadEvent = new AutoResetEvent(false);
            var internalCancelToken = new CancellationTokenSource();

            var results = new ConcurrentDictionary<int, T>();
            var resultReadIndex = new[] { -1 };

            var finalCount = -1;

            var readTimes = new List<DateTime>();
            readTimes.Add(DateTime.UtcNow);

            var lookAheadThread = new Thread(() =>
            {
                // calculate how far to look-ahead based on how quickly the results are being read
                Func<long> targetIndex = () =>
                {
                    var firstReadTime = readTimes[0];
                    var readPerMillisecond = (float)(readTimes.Count / (DateTime.UtcNow - firstReadTime).TotalMilliseconds);
                    return resultReadIndex[0] + 1 + (int)(readPerMillisecond * 1000); // look ahead 1000 milliseconds
                };

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
                    while (indexLocal > targetIndex())
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

            // enumerate the results
            var i = 0;
            while (finalCount == -1 || i < finalCount)
            {
                T result;
                try
                {
                    resultReadEvent.Set();

                    while (i >= results.Count && (finalCount == -1 || i < finalCount))
                    {
                        resultWriteEvent.WaitOne(TimeSpan.FromMilliseconds(10));
                    }
                    if (finalCount != -1 && i >= finalCount)
                        break;

                    result = results[i];
                    results[i] = default(T);
                }
                catch (Exception)
                {
                    internalCancelToken.Cancel();
                    lookAheadThread.Join();

                    // continue with exception
                    throw;
                }

                // yield result
                yield return result;

                resultReadIndex[0] = i;
                resultReadEvent.Set();
                i++;

                // store time the result was read
                readTimes.Add(DateTime.UtcNow);
                while (readTimes.Count > 500)
                    readTimes.RemoveAt(0);
            }

            internalCancelToken.Cancel();
            lookAheadThread.Join();
        }
    }
}
