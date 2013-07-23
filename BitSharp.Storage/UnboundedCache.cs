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

        private readonly string _name;
        protected readonly Func<TValue, long> sizeEstimator;

        // flush pending list
        private readonly ConcurrentDictionary<TKey, WriteValue<TValue>> flushPending;
        private long flushPendingSize;

        private CancellationTokenSource shutdownToken;

        private readonly Worker storageWorker;
        private readonly ManualResetEventSlim storageBlockEvent;

        private readonly MemoryCache<TKey, TValue> memoryCache;

        private readonly IUnboundedStorage<TKey, TValue> dataStorage;

        public UnboundedCache(string name, IUnboundedStorage<TKey, TValue> dataStorage, long maxFlushMemorySize, long maxCacheMemorySize, Func<TValue, long> sizeEstimator)
        {
            this._name = name;
            this.MaxFlushMemorySize = maxFlushMemorySize;

            this.flushPending = new ConcurrentDictionary<TKey, WriteValue<TValue>>();
            this.flushPendingSize = 0;

            this.memoryCache = new MemoryCache<TKey, TValue>(name, maxCacheMemorySize, sizeEstimator);
            this.MaxCacheMemorySize = maxCacheMemorySize;

            this.dataStorage = dataStorage;
            this.sizeEstimator = sizeEstimator;

            this.storageWorker = new Worker("{0}.StorageWorker".Format2(name), StorageWorker, true, TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(60));
            this.storageBlockEvent = new ManualResetEventSlim(true);

            this.shutdownToken = new CancellationTokenSource();

            // start storage thread, responsible for flushing out blocks
            this.storageWorker.Start();
        }

        public string Name { get { return this._name; } }

        public long MaxFlushMemorySize { get; set; }

        public long MaxCacheMemorySize
        {
            get { return this.memoryCache.MaxSize; }
            set { this.memoryCache.MaxSize = value; }
        }

        protected long FlushPendingSize { get { return this.flushPendingSize; } }

        protected long MemoryCacheSize { get { return this.memoryCache.CurrentSize; } }

        public void Dispose()
        {
            this.shutdownToken.Cancel();

            new IDisposable[]
            {
                this.storageWorker,
                this.memoryCache,
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
            if (TryGetMemoryValue(key, out value))
                return true;

            //Debug.WriteLine("{0}: Cache miss".Format2(this.name));

            // look in storage last
            if (TryGetStorageValue(key, out value, saveInCache))
                return true;

            // no value found in flush-pending, memory cache, or storage
            // fire missing event
            var handler = this.OnMissing;
            if (handler != null)
                handler(key);

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

        protected bool TryGetMemoryValue(TKey key, out TValue value)
        {
            return this.memoryCache.TryGetValue(key, out value);
        }

        protected bool TryGetStorageValue(TKey key, out TValue value, bool saveInCache)
        {
            TValue storedValue;
            if (dataStorage.TryReadValue(key, out storedValue))
            {
                // cache the retrieved value
                if (saveInCache)
                    CacheValue(key, storedValue);

                // fire retrieved event
                var handler = this.OnRetrieved;
                if (handler != null)
                    handler(key, storedValue);

                value = storedValue;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        protected void CacheValue(TKey key, TValue value)
        {
            this.memoryCache.CacheValue(key, value);
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

        protected virtual void BeforeCreateOrUpdate(TKey key, TValue value, bool isCreate)
        { }

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

            BeforeCreateOrUpdate(key, value, isCreate);

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

        // wait for all pending values to be flushed to storage
        public void WaitForStorageFlush()
        {
            this.storageWorker.ForceWorkAndWait();
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
    }
}
