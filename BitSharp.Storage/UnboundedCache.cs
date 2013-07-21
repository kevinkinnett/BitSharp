using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    //TODO add shared key storage so that things like BlockHeaderSqlStorage and BlockSqlStorage share a single view of keys on the same underlying table

    public class UnboundedCache<TKey, TValue> : IDisposable
    {
        public event Action<TKey> OnAddition;
        public event Action<TKey, TValue> OnModification;
        public event Action<TKey, TValue> OnRetrieved;
        public event Action<TKey> OnMissing;
        public event Action OnClear;

        private readonly string _name;
        protected readonly Func<TValue, long> sizeEstimator;

        // flush pending list
        private readonly ConcurrentDictionary<TKey, WriteValue<TValue>> flushPending;
        private long flushPendingSize;

        // memory cache
        private readonly ReaderWriterLockSlim memoryCacheLock;
        private readonly ConcurrentDictionary<CacheKey<TKey>, TValue> memoryCache;
        private long memoryCacheSize;
        private long cacheIndex;

        private CancellationTokenSource shutdownToken;

        private readonly Worker storageWorker;
        private readonly ManualResetEventSlim storageBlockEvent;

        private readonly IUnboundedStorage<TKey, TValue> dataStorage;

        private readonly Worker cacheWorker;
        private readonly ManualResetEventSlim cacheBlockEvent;

        public UnboundedCache(string name, IUnboundedStorage<TKey, TValue> dataStorage, long maxFlushMemorySize, long maxCacheMemorySize, Func<TValue, long> sizeEstimator)
        {
            this._name = name;
            this.MaxFlushMemorySize = maxFlushMemorySize;
            this.MaxCacheMemorySize = maxCacheMemorySize;

            this.flushPending = new ConcurrentDictionary<TKey, WriteValue<TValue>>();
            this.flushPendingSize = 0;

            this.memoryCacheLock = new ReaderWriterLockSlim();
            this.memoryCache = new ConcurrentDictionary<CacheKey<TKey>, TValue>();
            this.memoryCacheSize = 0;

            this.dataStorage = dataStorage;
            this.sizeEstimator = sizeEstimator;

            this.storageWorker = new Worker("{0}.StorageWorker".Format2(name), StorageWorker, true, TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(60));
            this.storageBlockEvent = new ManualResetEventSlim(true);

            this.cacheWorker = new Worker("{0}.CacheWorker".Format2(name), CacheWorker, true, TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(5));
            this.cacheBlockEvent = new ManualResetEventSlim(true);

            this.shutdownToken = new CancellationTokenSource();

            // start storage thread, responsible for flushing out blocks
            this.storageWorker.Start();

            // start cache thread, responsible for monitoring memory usage of block and transaction caches
            this.cacheWorker.Start();
        }

        public string Name { get { return this._name; } }

        public long MaxFlushMemorySize { get; set; }

        public long MaxCacheMemorySize { get; set; }

        protected long FlushPendingSize { get { return this.flushPendingSize; } }

        protected long MemoryCacheSize { get { return this.memoryCacheSize; } }

        public void Dispose()
        {
            this.shutdownToken.Cancel();

            new IDisposable[]
            {
                this.storageWorker,
                this.cacheWorker,
                this.shutdownToken
            }.DisposeList();
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> GetPendingValues()
        {
            foreach (var value in this.flushPending)
                yield return new KeyValuePair<TKey, TValue>(value.Key, value.Value.Value);
        }

        // try to get a value
        public bool TryGetValue(TKey key, out TValue value, bool saveInCache = true)
        {
            // check in flush-pending list first
            if (TryGetPendingValue(key, out value))
                return true;

            // look in the cache next, it will be added here before being removed from pending
            this.memoryCacheLock.EnterReadLock();
            try
            {
                TValue cachedValue;
                if (this.memoryCache.TryGetValue((CacheKey<TKey>)key, out cachedValue))
                {
                    value = cachedValue;
                    return true;
                }
            }
            finally
            {
                this.memoryCacheLock.ExitReadLock();
            }

            //Debug.WriteLine("{0}: Cache miss".Format2(this.name));

            // look in storage last
            TValue storedValue;
            if (dataStorage.TryReadValue(key, out storedValue))
            {
                // cache the retrieved value
                if (saveInCache)
                    CacheValue(key, storedValue);

                // fire missing event
                var handler1 = this.OnRetrieved;
                if (handler1 != null)
                    handler1(key, storedValue);

                value = storedValue;
                return true;
            }

            // no value found in flush-pending, memory cache, or storage
            // fire missing event
            var handler2 = this.OnMissing;
            if (handler2 != null)
                handler2(key);

            value = default(TValue);
            return false;
        }

        public void CreateValue(TKey key, TValue value)
        {
            CreateOrUpdateValue(key, value, isCreate: true);
        }

        public void UpdateValue(TKey key, TValue value)
        {
            CreateOrUpdateValue(key, value, isCreate: false);
        }

        protected bool TryGetPendingValue(TKey key, out TValue value)
        {
            WriteValue<TValue> pendingValue;
            if (this.flushPending.TryGetValue(key, out pendingValue))
            {
                value = pendingValue.Value;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        protected void RaiseOnAddition(TKey key)
        {
            var handler = this.OnAddition;
            if (handler != null)
                handler(key);
        }

        private bool IsPendingOversized
        {
            get { return this.flushPendingSize > this.MaxFlushMemorySize; }
        }

        private bool IsPendingExcessivelyOversized
        {
            get { return this.flushPendingSize > this.MaxFlushMemorySize * 1.25; }
        }

        private bool IsCacheOversized
        {
            get { return this.memoryCacheSize > this.MaxCacheMemorySize; }
        }

        private bool IsCacheExcessivelyOversized
        {
            get { return this.memoryCacheSize > this.MaxCacheMemorySize * 1.25; }
        }

        // create a new value, will be cached and flushed out to storage
        private void CreateOrUpdateValue(TKey key, TValue value, bool isCreate)
        {
            // force a storage flush if it is currently oversize
            if (this.IsPendingOversized)
            {
                if (this.IsPendingExcessivelyOversized)
                    this.storageBlockEvent.Reset();

                this.storageWorker.ForceWork();

                // block if flush list is excessively oversized
                while (!this.storageBlockEvent.Wait(TimeSpan.FromMilliseconds(50)) && !this.shutdownToken.IsCancellationRequested)
                { }
            }

            // init
            var memoryDelta = 0L;
            var flushValue = new WriteValue<TValue>(value, isCreate);

            var wasChanged = false;

            // create
            if (isCreate)
            {
                // only add to flush pending list, don't replace
                if (this.flushPending.TryAdd(key, flushValue))
                {
                    // add size of new value to memory size delta
                    memoryDelta += sizeEstimator(value);
                    wasChanged = true;
                }
            }
            // update
            else
            {
                // remove an existing value
                WriteValue<TValue> existingValue;
                if (this.flushPending.TryRemove(key, out existingValue))
                {
                    // remove size of existing value from memory size delta
                    memoryDelta -= this.sizeEstimator(existingValue.Value);
                    wasChanged = true;
                }

                // add new value
                if (this.flushPending.TryAdd(key, flushValue))
                {
                    // add size of new value to memory size delta
                    memoryDelta += sizeEstimator(value);
                    wasChanged = true;
                }
            }

            // update memory size
            Interlocked.Add(ref this.flushPendingSize, memoryDelta);

            // notify storage worker
            this.storageWorker.NotifyWork();

            // fire addition or modification event
            if (wasChanged)
            {
                if (isCreate)
                {
                    var handler = this.OnAddition;
                    if (handler != null)
                        handler(key);
                }
                else
                {
                    var handler = this.OnModification;
                    if (handler != null)
                        handler(key, value);
                }
            }
        }

        // clear all state and reload
        public void Clear()
        {
            // clear memory cache
            this.memoryCacheLock.DoWrite(() =>
            {
                this.memoryCache.Clear();
                this.memoryCacheSize = 0;
            });

            // fire cleared event
            var handler = this.OnClear;
            if (handler != null)
                handler();
        }

        // wait for all pending values to be flushed to storage
        public void WaitForStorageFlush()
        {
            this.storageWorker.ForceWorkAndWait();
        }

        // add a value to the memory cache
        protected void CacheValue(TKey key, TValue value)
        {
            if (MaxCacheMemorySize <= 0)
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

            this.memoryCacheLock.DoRead(() =>
            {
                // remove existing value
                TValue existingValue;
                if (this.memoryCache.TryRemove((CacheKey<TKey>)key, out existingValue))
                {
                    // remove existing value's size from the memory size delta
                    memoryDelta -= this.sizeEstimator(existingValue);
                }

                // add the new value to the cache
                var cacheIndex = Interlocked.Increment(ref this.cacheIndex);
                if (this.memoryCache.TryAdd(new CacheKey<TKey>(key, cacheIndex), value))
                {
                    // add size of new value to memory size delta
                    memoryDelta += sizeEstimator(value);
                }

                // indicate to cache that a new value is available so size can be checked
                Interlocked.Add(ref this.memoryCacheSize, memoryDelta);
            });

            // notify cache worker
            this.cacheWorker.NotifyWork();
        }

        // remove a value from the memory cache
        private void DecacheValue(TKey key)
        {
            var memoryDelta = 0L;

            this.memoryCacheLock.DoRead(() =>
            {
                // remove existing value
                TValue existingValue;
                if (this.memoryCache.TryRemove((CacheKey<TKey>)key, out existingValue))
                {
                    // remove existing value's size from the memory size delta
                    memoryDelta -= this.sizeEstimator(existingValue);
                }

                // indicate to cache that a new value is available so size can be checked
                Interlocked.Add(ref this.memoryCacheSize, memoryDelta);
            });
        }

        // storage worker thread, flush values to storage
        private void StorageWorker()
        {
            // check for pending data
            if (this.flushPending.Count > 0)
            {
                // block threads on an excess storage flush
                if (this.IsPendingExcessivelyOversized)
                    this.storageBlockEvent.Reset();

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var sizeDelta = 0L;

                // grab a snapshot
                var flushPendingLocal = this.flushPending.ToDictionary(x => x.Key, x => x.Value);

                // try to flush to storage
                bool result;
                try
                {
                    result = this.dataStorage.TryWriteValues(flushPendingLocal);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("{0}: UnboundedCache encountered exception during flush: {1}".Format2(this._name, e.Message));
                    Debugger.Break();
                    result = false;
                }

                if (result)
                {
                    // success

                    // remove values from pending list, unless they have been updated
                    foreach (var keyPair in flushPendingLocal)
                    {
                        var key = keyPair.Key;
                        var flushedValue = keyPair.Value;

                        // try to remove the flushed value
                        WriteValue<TValue> writeValue;
                        if (this.flushPending.TryRemove(key, out writeValue))
                        {
                            var valueSize = this.sizeEstimator(writeValue.Value);
                            sizeDelta -= valueSize;

                            // check that the removed value matches the flushed value
                            if (flushedValue.Guid != writeValue.Guid)
                            {
                                // mismatch, try to add the flush value back
                                if (this.flushPending.TryAdd(key, writeValue))
                                {
                                    sizeDelta += valueSize;
                                }
                            }
                        }
                    }

                    // after flush, cache the stored values
                    foreach (var keyPair in flushPendingLocal)
                        CacheValue(keyPair.Key, keyPair.Value.Value);
                }

                // update pending size based on changes made
                Interlocked.Add(ref this.flushPendingSize, sizeDelta);

                stopwatch.Stop();
                //Debug.WriteLine("{0,25} StorageWorker flushed {1,3:#,##0} items, {2,6:#,##0} KB in {3,6:#,##0} ms".Format2(this.Name + ":", flushPendingLocal.Count, (float)-sizeDelta / 1.THOUSAND(), stopwatch.ElapsedMilliseconds));
            }

            // unblock any threads waiting on an excess storage flush
            if (!this.IsPendingExcessivelyOversized)
                this.storageBlockEvent.Set();
        }

        // cache worker thread, monitor memory usage and dump as necessary
        private void CacheWorker()
        {
            // check if cache is oversized
            if (this.memoryCacheSize > this.MaxCacheMemorySize)
            {
                // block threads on an excess cache flush
                if (this.IsCacheExcessivelyOversized)
                    this.cacheBlockEvent.Reset();

                this.memoryCacheLock.DoRead(() =>
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    var trimmed = false;
                    var trimmedSize = 0L;

                    foreach (var key in this.memoryCache.Keys.OrderBy(x => x.Index))
                    {
                        // cooperative loop
                        this.shutdownToken.Token.ThrowIfCancellationRequested();

                        //Debug.WriteLine("{0}: Removing cache item at index {1:#,##0}".Format2(this.name, key.Index));

                        // when cache becomes oversized, remove items until a new target percentage is reached
                        if (this.memoryCacheSize <= this.MaxCacheMemorySize * 0.7)
                            break;

                        // remove a value
                        TValue value;
                        if (this.memoryCache.TryRemove(key, out value))
                        {
                            // calculate its size and remove from the memory cache size
                            var valueSize = this.sizeEstimator(value);
                            Interlocked.Add(ref this.memoryCacheSize, -valueSize);

                            // track cache removal statistics
                            trimmed = true;
                            trimmedSize += valueSize;
                        }
                    }

                    stopwatch.Stop();
                    if (trimmed)
                    {
                        //Debug.WriteLine("{0,25} CacheWorker trimmed {1:#,##0} bytes in {2:#,##0} ms, count: {3:#,##0}, size: {4:#,##0.000} MB, process memory: {5:#,##0.000} MB".Format2(this.name + ":", trimmedSize, stopwatch.ElapsedMilliseconds, this.memoryCache.Count, (float)this.memoryCacheSize / 1.MILLION(), (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()));
                    }
                });
            }

            // unblock any threads waiting on an excess cache flush
            if (!this.IsCacheExcessivelyOversized)
                this.cacheBlockEvent.Set();
        }
    }
}
