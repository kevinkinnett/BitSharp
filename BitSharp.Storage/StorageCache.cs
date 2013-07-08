using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.WireProtocol;
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
    //TODO add shared key storage so that things like BlockHeaderSqlStorage and BlockDataSqlStorage share a single view of keys on the same underlying table

    public class StorageCache<TKey, TValue> : IDisposable
    {
        public event Action OnClear;
        public event Action<TKey> OnAddition;
        public event Action<TKey> OnModification;
        public event Action<TKey> OnRemoval;

        private readonly string name;
        private readonly Func<TValue, long> sizeEstimator;

        // flush pending list
        private readonly ConcurrentDictionary<TKey, WriteValue<TValue>> flushPending;
        private long flushPendingSize;

        // memory cache
        private readonly ReaderWriterLock memoryCacheLock;
        private readonly ConcurrentDictionary<CacheKey<TKey>, TValue> memoryCache;
        private long memoryCacheSize;
        private long cacheIndex;

        // known keys
        private readonly ReaderWriterLock knownKeysLock;
        private ConcurrentSet<TKey> knownKeys;

        private bool shutdownThreads;

        private Thread storageThread;
        private readonly ThrottledNotifyEvent storageNotifyEvent;
        private readonly ManualResetEvent storageWorkerIdleEvent;
        private readonly ManualResetEvent storageOversizeEvent;

        private readonly IDataStorage<TKey, TValue> dataStorage;

        private Thread cacheThread;
        private readonly ThrottledNotifyEvent cacheNotifyEvent;
        private readonly ManualResetEvent cacheOversizeEvent;

        public StorageCache(string name, IDataStorage<TKey, TValue> dataStorage, long maxFlushMemorySize, long maxCacheMemorySize, Func<TValue, long> sizeEstimator)
        {
            this.name = name;
            this.MaxFlushMemorySize = maxFlushMemorySize;
            this.MaxCacheMemorySize = maxCacheMemorySize;

            this.flushPending = new ConcurrentDictionary<TKey, WriteValue<TValue>>();
            this.flushPendingSize = 0;

            this.memoryCacheLock = new ReaderWriterLock();
            this.memoryCache = new ConcurrentDictionary<CacheKey<TKey>, TValue>();
            this.memoryCacheSize = 0;

            this.knownKeysLock = new ReaderWriterLock();
            this.knownKeys = new ConcurrentSet<TKey>();

            this.dataStorage = dataStorage;
            this.sizeEstimator = sizeEstimator;

            this.storageNotifyEvent = new ThrottledNotifyEvent(false, TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(60));
            this.storageWorkerIdleEvent = new ManualResetEvent(false);
            this.storageOversizeEvent = new ManualResetEvent(false);

            this.cacheNotifyEvent = new ThrottledNotifyEvent(false, TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(5));
            this.cacheOversizeEvent = new ManualResetEvent(false);

            // load existing keys from storage
            LoadKeyFromStorage();

            this.shutdownThreads = false;

            // start storage thread, responsible for flushing out blocks
            this.storageThread = new Thread(StorageWorker);
            this.storageThread.Start();

            // start cache thread, responsible for monitoring memory usage of block and transaction caches
            this.cacheThread = new Thread(CacheWorker);
            this.cacheThread.Start();
        }

        public long MaxFlushMemorySize { get; set; }

        public long MaxCacheMemorySize { get; set; }

        public void Dispose()
        {
            this.shutdownThreads = true;

            this.storageNotifyEvent.ForceSet();
            this.cacheNotifyEvent.ForceSet();

            this.storageThread.Join();
            this.cacheThread.Join();
        }

        // get count of known items
        public int Count
        {
            get { return this.knownKeys.Count; }
        }

        public bool ContainsKey(TKey key)
        {
            return this.knownKeys.Contains(key);
        }

        // get all known item keys
        public IEnumerable<TKey> GetAllKeys()
        {
            //TODO could this ever return a duplicate key?
            foreach (var key in this.knownKeys)
                yield return key;
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> GetPendingValues()
        {
            foreach (var value in this.flushPending)
                yield return new KeyValuePair<TKey, TValue>(value.Key, value.Value.Value);
        }

        // get all known item keys, reads everything from storage
        public IEnumerable<TKey> GetAllKeysFromStorage()
        {
            var returnedKeys = new HashSet<TKey>();

            foreach (var key in this.dataStorage.ReadAllKeys())
            {
                returnedKeys.Add(key);
                AddKnownKey(key);
                yield return key;
            }

            // ensure that any keys not returned from storage are returned as well, pending items
            var pendingKeys = this.knownKeys.Except(returnedKeys);
            foreach (var key in pendingKeys)
                yield return key;
        }

        // get all values, reads everything from storage
        public IEnumerable<KeyValuePair<TKey, TValue>> StreamAllValues()
        {
            var returnedKeys = new Dictionary<TKey, object>();

            // return and track items from flush pending list
            // ensure a key is never returned twice in case modifications are made during the enumeration
            foreach (var flushKeyPair in this.flushPending)
            {
                returnedKeys.Add(flushKeyPair.Key, null);
                yield return new KeyValuePair<TKey, TValue>(flushKeyPair.Key, flushKeyPair.Value.Value);
            }

            // return items from storage, still ensuring a key is never returned twice
            // storage doesn't need to add to returnedKeys as storage items will always be returned uniquely
            foreach (var storageKeyPair in this.dataStorage.ReadAllValues())
            {
                if (!returnedKeys.ContainsKey(storageKeyPair.Key))
                {
                    // make sure any keys found in storage become known
                    AddKnownKey(storageKeyPair.Key);

                    yield return storageKeyPair;
                }
            }
        }

        // try to get a value
        public bool TryGetValue(TKey key, out TValue value, bool saveInCache = true)
        {
            // check in flush-pending list first
            WriteValue<TValue> pendingValue;
            if (this.flushPending.TryGetValue(key, out pendingValue))
            {
                value = pendingValue.Value;
                return true;
            }

            // look in the cache next, it will be added here before being removed from pending
            this.memoryCacheLock.AcquireReaderLock(int.MaxValue);
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
                this.memoryCacheLock.ReleaseReaderLock();
            }

            //Debug.WriteLine("{0}: Cache miss".Format2(this.name));

            // look in storage last
            TValue storedValue;
            if (dataStorage.TryReadValue(key, out storedValue))
            {
                // cache the retrieved value
                if (saveInCache)
                    CacheValue(key, storedValue);

                value = storedValue;
                return true;
            }

            // no value found in flush-pending, memory cache, or storage
            // it does not exist
            RemoveKnownKey(key);

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
            // force and wait for a storage flush if it is currently oversize
            if (this.IsPendingOversized)
            {
                this.storageOversizeEvent.Reset();
                this.storageNotifyEvent.ForceSet();

                // only block if storage becomes excessively oversized
                if (this.IsPendingExcessivelyOversized)
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Debug.WriteLine("{0,25} CacheWorker blocking flush pending additions due to excess size".Format2(this.name));

                    //TODO can get stuck on thread shutdown
                    this.storageOversizeEvent.WaitOne();

                    stopwatch.Stop();
                    Debug.WriteLine("{0,25} CacheWorker unblocking flush pending additions: {0:#,##0.000}s".Format2(this.name, stopwatch.EllapsedSecondsFloat()));
                }
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

            // add key to known list
            AddKnownKey(key);

            // notify storage worker that a new value is available to be flushed
            if (wasChanged)
                this.storageNotifyEvent.Set();

            // fire modification event, AddKnownKey will have already fired an addition event
            if (wasChanged && !isCreate)
            {
                var handler = this.OnModification;
                if (handler != null)
                    handler(key);
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

            // clear known keys
            this.knownKeysLock.DoWrite(() =>
            {
                this.knownKeys = new ConcurrentSet<TKey>();
            });

            // reload existing keys from storage
            LoadKeyFromStorage();

            // fire cleared event
            var handler = this.OnClear;
            if (handler != null)
                handler();
        }

        // wait for all pending values to be flushed to storage
        public void WaitForStorageFlush()
        {
            // wait for worker to idle
            this.storageWorkerIdleEvent.WaitOne();

            // reset its idle state
            this.storageWorkerIdleEvent.Reset();

            // force an execution
            this.storageNotifyEvent.ForceSet();

            // wait for worker to be idle again
            this.storageWorkerIdleEvent.WaitOne();
        }

        // load all existing keys from storage
        private void LoadKeyFromStorage()
        {
            var count = 0;
            foreach (var key in this.dataStorage.ReadAllKeys())
            {
                AddKnownKey(key);
                count++;
            }
            Debug.WriteLine("{0}: Finished loading from storage: {1:#,##0}".Format2(this.name, count));
        }

        // add a key to the known list, fire event if new
        private long keyChurn = 0;
        private void AddKnownKey(TKey key)
        {
            var wasAdded = false;

            this.knownKeysLock.DoWrite(() =>
            {
                // add to the list of known keys
                if (this.knownKeys.TryAdd(key))
                {
                    wasAdded = true;

                    Interlocked.Increment(ref this.keyChurn);
                    if (this.keyChurn % 100.THOUSAND() == 0)
                        this.knownKeys = new ConcurrentSet<TKey>(this.knownKeys);
                }
            });

            // fire addition event
            if (wasAdded)
            {
                var handler = this.OnAddition;
                if (handler != null)
                    handler(key);
            }
        }

        // remove a key from the known list, fire event if deleted
        private void RemoveKnownKey(TKey key)
        {
            var wasRemoved = false;

            // remove from the list of known keys
            this.knownKeysLock.DoWrite(() =>
            {
                if (this.knownKeys.TryRemove(key))
                {
                    wasRemoved = true;

                    Interlocked.Increment(ref this.keyChurn);
                    if (this.keyChurn % 100.THOUSAND() == 0)
                        this.knownKeys = new ConcurrentSet<TKey>(this.knownKeys);
                }
            });

            // fire removal event
            if (wasRemoved)
            {
                var handler = this.OnRemoval;
                if (handler != null)
                    handler(key);
            }
        }

        //TODO memory cache doesn't take into account create/update
        // add a value to the memory cache
        private void CacheValue(TKey key, TValue value)
        {
            if (MaxCacheMemorySize <= 0)
                return;

            // force a cache flush if it is currently oversize
            if (this.IsCacheOversized)
            {
                this.cacheOversizeEvent.Reset();
                this.cacheNotifyEvent.ForceSet();

                // only block if cache becomes excessively oversized
                if (this.IsCacheExcessivelyOversized)
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Debug.WriteLine("{0,25} CacheWorker blocking cache additions due to excess size".Format2(this.name));

                    //TODO can get stuck on thread shutdown
                    this.cacheOversizeEvent.WaitOne();

                    stopwatch.Stop();
                    Debug.WriteLine("{0,25} CacheWorker unblocking cache additions: {0:#,##0.000}s".Format2(this.name, stopwatch.EllapsedSecondsFloat()));
                }
            }

            // ensure this value is known when added to cache
            AddKnownKey(key);

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

            // whenever cache goes over size, notify cache worker
            if (this.IsCacheOversized)
            {
                this.cacheNotifyEvent.Set();
            }
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
            try
            {
                while (!this.shutdownThreads)
                {
                    // wait for work notification
                    this.storageWorkerIdleEvent.Set();
                    this.storageNotifyEvent.WaitOne();

                    // notify that work is starting
                    this.storageWorkerIdleEvent.Reset();

                    // check for pending data
                    if (this.flushPending.Count > 0)
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();

                        var sizeDelta = 0L;

                        // grab a snapshot
                        var flushPendingLocal = this.flushPending.ToDictionary(x => x.Key, x => x.Value);

                        // try to flush to storage
                        if (this.dataStorage.TryWriteValues(flushPendingLocal))
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

                            // after flush, decache values so they can be reread from storage on next access
                            foreach (var key in flushPendingLocal.Keys)
                                DecacheValue(key);
                        }

                        // update pending size based on changes made
                        Interlocked.Add(ref this.flushPendingSize, sizeDelta);

                        stopwatch.Stop();
                        Debug.WriteLine("{0,25} StorageWorker flushed {1:#,##0} items, {2:#,##0.000} KB in {3:#,##0} ms".Format2(this.name + ":", flushPendingLocal.Count, (float)-sizeDelta / 1.THOUSAND(), stopwatch.ElapsedMilliseconds));
                    }

                    // unblock any threads waiting on an excess storage flush
                    if (!this.IsPendingExcessivelyOversized)
                    {
                        this.storageOversizeEvent.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("{0}: DataCache encountered fatal exception in StorageWorker: {1}\n\n{2}", this.name, e.Message, e));
                Debugger.Break();
                Environment.Exit(-2);
            }
        }

        // cache worker thread, monitor memory usage and dump as necessary
        private void CacheWorker()
        {
            try
            {
                while (!this.shutdownThreads)
                {
                    // wait for work notification
                    this.cacheNotifyEvent.WaitOne();

                    // check if cache is oversized
                    if (this.memoryCacheSize > this.MaxCacheMemorySize)
                    {
                        this.memoryCacheLock.DoRead(() =>
                        {
                            var stopwatch = new Stopwatch();
                            stopwatch.Start();

                            var trimmed = false;
                            var trimmedSize = 0L;

                            foreach (var key in this.memoryCache.Keys.OrderBy(x => x.Index))
                            {
                                // cooperative loop
                                if (this.shutdownThreads)
                                {
                                    this.cacheOversizeEvent.Set(); // ensure no threads stay blocked
                                    return;
                                }

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
                            Debug.Assert(this.memoryCacheSize >= 0);

                            if (trimmed)
                            {
                                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);

                                stopwatch.Stop();
                                //Debug.WriteLine("{0,25} CacheWorker trimmed {1:#,##0} bytes in {2:#,##0} ms, count: {3:#,##0}, size: {4:#,##0.000} MB, process memory: {5:#,##0.000} MB".Format2(this.name + ":", trimmedSize, stopwatch.ElapsedMilliseconds, this.memoryCache.Count, (float)this.memoryCacheSize / 1.MILLION(), (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()));
                            }
                        });
                    }

                    // unblock any threads waiting on an excess cache flush
                    if (!this.IsCacheExcessivelyOversized)
                    {
                        this.cacheOversizeEvent.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("{0}: DataCache encountered fatal exception in CacheWorker: {1}\n\n{2}", this.name, e.Message, e));
                Debugger.Break();
                Environment.Exit(-2);
            }
        }
    }
}
