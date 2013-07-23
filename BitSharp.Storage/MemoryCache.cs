using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class MemoryCache<TKey, TValue> : IDisposable
    {
        public event Action OnClear;
        
        private readonly string _name;
        protected readonly Func<TValue, long> sizeEstimator;

        // memory cache
        private readonly ReaderWriterLockSlim cacheLock;
        private readonly ConcurrentDictionary<CacheKey<TKey>, TValue> cache;
        private long _currentSize;
        private long cacheIndex;

        private CancellationTokenSource shutdownToken;

        private readonly Worker cacheWorker;
        private readonly ManualResetEventSlim cacheBlockEvent;

        public MemoryCache(string name, long maxSize, Func<TValue, long> sizeEstimator)
        {
            this._name = name;
            this.MaxSize = maxSize;

            this.cacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            this.cache = new ConcurrentDictionary<CacheKey<TKey>, TValue>();
            this._currentSize = 0;

            this.sizeEstimator = sizeEstimator;

            this.cacheWorker = new Worker("MemoryCache.{0}.CacheWorker".Format2(name), CacheWorker, true, TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(5));
            this.cacheBlockEvent = new ManualResetEventSlim(true);

            this.shutdownToken = new CancellationTokenSource();

            // start cache thread, responsible for monitoring memory usage of block and transaction caches
            this.cacheWorker.Start();
        }

        public string Name { get { return this._name; } }

        public long CurrentSize { get { return this._currentSize; } }

        public long MaxSize { get; set; }

        public void Dispose()
        {
            this.shutdownToken.Cancel();
            
            new IDisposable[]
            {
                this.cacheWorker,
                this.cacheBlockEvent,
                this.cacheLock,
                this.shutdownToken
            }.DisposeList();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            this.cacheLock.EnterReadLock();
            try
            {
                TValue cachedValue;
                if (this.cache.TryGetValue((CacheKey<TKey>)key, out cachedValue))
                {
                    value = cachedValue;
                    return true;
                }
                else
                {
                    value = default(TValue);
                    return false;
                }
            }
            finally
            {
                this.cacheLock.ExitReadLock();
            }
        }

        // clear all state and reload
        public void Clear()
        {
            // clear memory cache
            this.cacheLock.DoWrite(() =>
            {
                this.cache.Clear();
                this._currentSize = 0;
            });

            // fire cleared event
            var handler = this.OnClear;
            if (handler != null)
                handler();
        }

        // add a value to the memory cache
        public void CacheValue(TKey key, TValue value)
        {
            if (MaxSize <= 0)
                return;

            // force a cache flush if it is currently oversize
            if (this.IsCacheOversized)
            {
                if (this.IsCacheExcessivelyOversized)
                    this.cacheBlockEvent.Reset();

                this.cacheWorker.ForceWork();

                // block if cache is excessively oversized
                while (!this.cacheBlockEvent.Wait(TimeSpan.FromMilliseconds(50)) && !this.shutdownToken.IsCancellationRequested)
                { }
            }

            var memoryDelta = 0L;

            this.cacheLock.DoWrite(() =>
            {
                // remove existing value
                TValue existingValue;
                if (this.cache.TryRemove((CacheKey<TKey>)key, out existingValue))
                {
                    // remove existing value's size from the memory size delta
                    memoryDelta -= this.sizeEstimator(existingValue);
                }

                // add the new value to the cache
                var cacheIndex = Interlocked.Increment(ref this.cacheIndex);
                if (this.cache.TryAdd(new CacheKey<TKey>(key, cacheIndex), value))
                {
                    // add size of new value to memory size delta
                    memoryDelta += sizeEstimator(value);
                }

                // indicate to cache that a new value is available so size can be checked
                Interlocked.Add(ref this._currentSize, memoryDelta);
            });

            // notify cache worker
            this.cacheWorker.NotifyWork();
        }

        // remove a value from the memory cache
        protected void DecacheValue(TKey key)
        {
            var memoryDelta = 0L;

            this.cacheLock.DoWrite(() =>
            {
                // remove existing value
                TValue existingValue;
                if (this.cache.TryRemove((CacheKey<TKey>)key, out existingValue))
                {
                    // remove existing value's size from the memory size delta
                    memoryDelta -= this.sizeEstimator(existingValue);
                }

                // indicate to cache that a new value is available so size can be checked
                Interlocked.Add(ref this._currentSize, memoryDelta);
            });
        }

        private bool IsCacheOversized
        {
            get { return this.CurrentSize > this.MaxSize; }
        }

        private bool IsCacheExcessivelyOversized
        {
            get { return this.CurrentSize > this.MaxSize * 1.25; }
        }

        // cache worker thread, monitor memory usage and dump as necessary
        private void CacheWorker()
        {
            // check if cache is oversized
            if (this.IsCacheOversized)
            {
                // block threads on an excess cache flush
                if (this.IsCacheExcessivelyOversized)
                    this.cacheBlockEvent.Reset();

                this.cacheLock.DoWrite(() =>
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    var trimmed = false;
                    var trimmedSize = 0L;

                    foreach (var key in this.cache.Keys.OrderBy(x => x.Index))
                    {
                        // cooperative loop
                        this.shutdownToken.Token.ThrowIfCancellationRequested();

                        //Debug.WriteLine("{0}: Removing cache item at index {1:#,##0}".Format2(this.name, key.Index));

                        // when cache becomes oversized, remove items until a new target percentage is reached
                        if (this._currentSize <= this.MaxSize * 0.7)
                            break;

                        // remove a value
                        TValue value;
                        if (this.cache.TryRemove(key, out value))
                        {
                            // calculate its size and remove from the memory cache size
                            var valueSize = this.sizeEstimator(value);
                            Interlocked.Add(ref this._currentSize, -valueSize);

                            // track cache removal statistics
                            trimmed = true;
                            trimmedSize += valueSize;
                        }
                    }

                    stopwatch.Stop();
                    if (trimmed)
                    {
                        //Debug.WriteLine("{0,25} MemoryCache trimmed {1:#,##0} bytes in {2:#,##0} ms, count: {3:#,##0}, size: {4:#,##0.000} MB, process memory: {5:#,##0.000} MB".Format2(this.name + ":", trimmedSize, stopwatch.ElapsedMilliseconds, this.memoryCache.Count, (float)this.memoryCacheSize / 1.MILLION(), (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()));
                    }
                });
            }

            // unblock any threads waiting on an excess cache flush
            if (!this.IsCacheExcessivelyOversized)
                this.cacheBlockEvent.Set();
        }
    }
}
