using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain;
using BitSharp.Database;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Storage.ExtensionMethods;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    //TODO have a class for building blockchain, and a class for cleaning?

    // blockchain rules here:
    //
    // https://github.com/bitcoin/bitcoin/blob/4ad73c6b080c46808b0c53b62ab6e4074e48dc75/src/main.cpp
    //
    // bool ConnectBlock(CBlock& block, CValidationState& state, CBlockIndex* pindex, CCoinsViewCache& view, bool fJustCheck)
    // https://github.com/bitcoin/bitcoin/blob/4ad73c6b080c46808b0c53b62ab6e4074e48dc75/src/main.cpp#L1734
    //
    //TODO BIP-030

    //TODO compact UTXO's and other immutables in the blockchains on a thread
    public class BlockchainDaemon : IBlockchainRetriever, IDisposable
    {
        public event EventHandler<BlockMetadata> OnWinningBlockChanged;
        public event EventHandler<Blockchain.Blockchain> OnCurrentBlockchainChanged;

        private readonly StorageManager _storageManager;

        private readonly IBlockchainRules _rules;
        private readonly BlockchainCalculator calculator;

        private BlockMetadata winningBlock;
        private Blockchain.Blockchain currentBlockchain;

        private readonly ConcurrentSet<UInt256> missingBlocks;
        private readonly ConcurrentSet<UInt256> missingBlockMetadata;
        private readonly ConcurrentSet<UInt256> missingTransactions;
        private readonly ConcurrentSet<UInt256> updateMetadataSet;

        private readonly CancellationTokenSource shutdownToken;

        private readonly Thread missingMetadataWorkerThread;
        private readonly ThrottledNotifyEvent missingMetadataWorkerNotifyEvent;
        private readonly ManualResetEvent missingMetadataWorkerIdleEvent;

        private readonly Thread metadataWorkerThread;
        private readonly ThrottledNotifyEvent metadataWorkerNotifyEvent;
        private readonly ManualResetEvent metadataWorkerIdleEvent;

        private readonly Thread chainingWorkerThread;
        private readonly ThrottledNotifyEvent chainingWorkerNotifyEvent;
        private readonly ManualResetEvent chainingWorkerIdleEvent;

        private readonly Thread validationWorkerThread;
        private readonly ThrottledNotifyEvent validationWorkerNotifyEvent;
        private readonly ManualResetEvent validationWorkerIdleEvent;

        private readonly Thread blockchainWorkerThread;
        private readonly ThrottledNotifyEvent blockchainWorkerNotifyEvent;
        private readonly ManualResetEvent blockchainWorkerIdleEvent;

        private readonly Thread validateCurrentChainWorkerThread;
        private readonly ThrottledNotifyEvent validateCurrentChainWorkerNotifyEvent;
        private readonly ManualResetEvent validateCurrentChainWorkerIdleEvent;

        public BlockchainDaemon(IBlockchainRules rules, StorageManager storageManager)
        {
            this.shutdownToken = new CancellationTokenSource();

            this._rules = rules;
            this._storageManager = storageManager;
            this.calculator = new BlockchainCalculator(this._rules, this, this.shutdownToken.Token);

            this.winningBlock = this._rules.GenesisBlockMetadata;
            this.currentBlockchain = this._rules.GenesisBlockchain;

            this.missingBlocks = new ConcurrentSet<UInt256>();
            this.missingBlockMetadata = new ConcurrentSet<UInt256>();
            this.missingTransactions = new ConcurrentSet<UInt256>();
            this.updateMetadataSet = new ConcurrentSet<UInt256>();

            // write genesis block out to storage
            this._storageManager.BlockDataCache.UpdateValue(this._rules.GenesisBlock.Hash, this._rules.GenesisBlock);
            this._storageManager.BlockMetadataCache.UpdateValue(this._rules.GenesisBlockMetadata.BlockHash, this._rules.GenesisBlockMetadata);

            //TODO
            this._storageManager.BlockDataCache.WaitForStorageFlush();
            this._storageManager.BlockMetadataCache.WaitForStorageFlush();

            // start loading the existing state from storage
            //LoadExistingState();

            // create worker notification events
            this.missingMetadataWorkerNotifyEvent = new ThrottledNotifyEvent(true, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
            this.metadataWorkerNotifyEvent = new ThrottledNotifyEvent(true, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
            this.chainingWorkerNotifyEvent = new ThrottledNotifyEvent(true, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
            this.validationWorkerNotifyEvent = new ThrottledNotifyEvent(true, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
            this.blockchainWorkerNotifyEvent = new ThrottledNotifyEvent(true, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(5));
            this.validateCurrentChainWorkerNotifyEvent = new ThrottledNotifyEvent(true, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));

            // create worker idle events
            this.missingMetadataWorkerIdleEvent = new ManualResetEvent(false);
            this.metadataWorkerIdleEvent = new ManualResetEvent(false);
            this.chainingWorkerIdleEvent = new ManualResetEvent(false);
            this.validationWorkerIdleEvent = new ManualResetEvent(false);
            this.blockchainWorkerIdleEvent = new ManualResetEvent(false);
            this.validateCurrentChainWorkerIdleEvent = new ManualResetEvent(false);

            this._storageManager.BlockDataCache.OnAddition += OnBlockDataAddition;
            this._storageManager.BlockDataCache.OnModification += OnBlockDataModification;
            this._storageManager.BlockMetadataCache.OnAddition += OnBlockMetadataAddition;
            this._storageManager.BlockMetadataCache.OnModification += OnBlockMetadataModification;

            //start workers
            this.missingMetadataWorkerThread = new Thread(MissingMetadataWorker);
            this.metadataWorkerThread = new Thread(MetadataWorker);
            this.chainingWorkerThread = new Thread(ChainingWorker);
            this.validationWorkerThread = new Thread(ValidationWorker);
            this.blockchainWorkerThread = new Thread(BlockchainWorker);
            this.validateCurrentChainWorkerThread = new Thread(ValidateCurrentChainWorker);
        }

        public IBlockchainRules Rules { get { return this._rules; } }

        public StorageManager StorageManager { get { return this._storageManager; } }

        public BlockMetadata WinningBlock { get { return this.winningBlock; } }

        public Blockchain.Blockchain CurrentBlockchain { get { return this.currentBlockchain; } }

        public ImmutableHashSet<UInt256> MissingBlocks
        {
            get
            {
                //TODO cache
                return this.missingBlocks.ToImmutableHashSet();
            }
        }

        public void Start()
        {
            try
            {
                this.missingMetadataWorkerThread.Start();
                this.metadataWorkerThread.Start();
                this.chainingWorkerThread.Start();
                this.validationWorkerThread.Start();
                this.blockchainWorkerThread.Start();
                this.validateCurrentChainWorkerThread.Start();
            }
            catch (Exception)
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // cleanup events
            this._storageManager.BlockDataCache.OnAddition -= OnBlockDataAddition;
            this._storageManager.BlockDataCache.OnModification -= OnBlockDataModification;
            this._storageManager.BlockMetadataCache.OnAddition -= OnBlockMetadataAddition;
            this._storageManager.BlockMetadataCache.OnModification -= OnBlockMetadataModification;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            this.missingMetadataWorkerNotifyEvent.ForceSet();
            this.metadataWorkerNotifyEvent.ForceSet();
            this.chainingWorkerNotifyEvent.ForceSet();
            this.validationWorkerNotifyEvent.ForceSet();
            this.blockchainWorkerNotifyEvent.ForceSet();
            this.validateCurrentChainWorkerNotifyEvent.ForceSet();

            //TODO Join() all threads simultaneously so that overall timeout isn't 240 seconds
            // grace period for threads to cleanly shut down
            var timeout = TimeSpan.FromSeconds(60);

            // cleanup missing metadata worker
            if (!this.missingMetadataWorkerThread.Join(timeout))
            {
                try { this.missingMetadataWorkerThread.Abort(); }
                catch (Exception) { }
            }
            
            // cleanup metadata worker
            if (!this.metadataWorkerThread.Join(timeout))
            {
                try { this.metadataWorkerThread.Abort(); }
                catch (Exception) { }
            }

            // cleanup chaining worker
            if (!this.chainingWorkerThread.Join(timeout))
            {
                try { this.chainingWorkerThread.Abort(); }
                catch (Exception) { }
            }

            // cleanup validation worker
            if (!this.validationWorkerThread.Join(timeout))
            {
                try { this.validationWorkerThread.Abort(); }
                catch (Exception) { }
            }

            // cleanup blockchain worker
            if (!this.blockchainWorkerThread.Join(timeout))
            {
                try { this.blockchainWorkerThread.Abort(); }
                catch (Exception) { }
            }

            // cleanup current blockchain revalidation worker
            if (!this.validateCurrentChainWorkerThread.Join(timeout))
            {
                try { this.validateCurrentChainWorkerThread.Abort(); }
                catch (Exception) { }
            }

            this.shutdownToken.Dispose();
        }

        public void WaitForFullUpdate()
        {
            WaitForMissingMetadataUpdate();
            WaitForMetadataUpdate();
            WaitForChainingUpdate();
            WaitForValidationUpdate();
            WaitForBlockchainUpdate();
        }

        public void WaitForMissingMetadataUpdate()
        {
            // wait for worker to idle
            this.missingMetadataWorkerIdleEvent.WaitOne();

            // reset its idle state
            this.missingMetadataWorkerIdleEvent.Reset();

            // force an execution
            this.missingMetadataWorkerNotifyEvent.ForceSet();

            // wait for worker to be idle again
            this.missingMetadataWorkerIdleEvent.WaitOne();
        }

        public void WaitForMetadataUpdate()
        {
            // wait for worker to idle
            this.metadataWorkerIdleEvent.WaitOne();

            // reset its idle state
            this.metadataWorkerIdleEvent.Reset();

            // force an execution
            this.metadataWorkerNotifyEvent.ForceSet();

            // wait for worker to be idle again
            this.metadataWorkerIdleEvent.WaitOne();
        }

        public void WaitForChainingUpdate()
        {
            // wait for worker to idle
            this.chainingWorkerIdleEvent.WaitOne();

            // reset its idle state
            this.chainingWorkerIdleEvent.Reset();

            // force an execution
            this.chainingWorkerNotifyEvent.ForceSet();

            // wait for worker to be idle again
            this.chainingWorkerIdleEvent.WaitOne();
        }

        public void WaitForValidationUpdate()
        {
            // wait for worker to idle
            this.validationWorkerIdleEvent.WaitOne();

            // reset its idle state
            this.validationWorkerIdleEvent.Reset();

            // force an execution
            this.validationWorkerNotifyEvent.ForceSet();

            // wait for worker to be idle again
            this.validationWorkerIdleEvent.WaitOne();
        }

        public void WaitForBlockchainUpdate()
        {
            // wait for worker to idle
            this.blockchainWorkerIdleEvent.WaitOne();

            // reset its idle state
            this.blockchainWorkerIdleEvent.Reset();

            // force an execution
            this.blockchainWorkerNotifyEvent.ForceSet();

            // wait for worker to be idle again
            this.blockchainWorkerIdleEvent.WaitOne();
        }

        public void WaitForBlockchainRevalidation()
        {
            // wait for worker to idle
            this.validateCurrentChainWorkerIdleEvent.WaitOne();

            // reset its idle state
            this.validateCurrentChainWorkerIdleEvent.Reset();

            // force an execution
            this.validateCurrentChainWorkerNotifyEvent.ForceSet();

            // wait for worker to be idle again
            this.validateCurrentChainWorkerIdleEvent.WaitOne();
        }

        private void OnBlockDataAddition(UInt256 blockHash)
        {
            this.missingBlocks.TryRemove(blockHash);

            this.updateMetadataSet.TryAdd(blockHash);

            this.metadataWorkerNotifyEvent.Set();
            this.chainingWorkerNotifyEvent.Set();
            this.blockchainWorkerNotifyEvent.Set();
        }

        private void OnBlockDataModification(UInt256 blockHash)
        {
            this.missingBlocks.TryRemove(blockHash);

            this.updateMetadataSet.TryAdd(blockHash);

            this.metadataWorkerNotifyEvent.Set();
            this.chainingWorkerNotifyEvent.Set();
            this.blockchainWorkerNotifyEvent.Set();
        }

        private void OnBlockMetadataAddition(UInt256 blockHash)
        {
            this.missingBlockMetadata.TryRemove(blockHash);

            this.updateMetadataSet.TryAdd(blockHash);

            this.metadataWorkerNotifyEvent.Set();
            //this.chainingWorkerNotifyEvent.Set();
            //this.validationWorkerNotifyEvent.Set();
            //this.blockchainWorkerNotifyEvent.Set();
        }

        private void OnBlockMetadataModification(UInt256 blockHash)
        {
            this.missingBlockMetadata.TryRemove(blockHash);

            this.updateMetadataSet.TryAdd(blockHash);

            this.metadataWorkerNotifyEvent.Set();
            //this.chainingWorkerNotifyEvent.Set();
            //this.validationWorkerNotifyEvent.Set();
            //this.blockchainWorkerNotifyEvent.Set();
        }

        private void MissingMetadataWorker()
        {
            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // wait for work notification
                    this.missingMetadataWorkerIdleEvent.Set();
                    this.missingMetadataWorkerNotifyEvent.WaitOne();

                    // notify that work is starting
                    this.missingMetadataWorkerIdleEvent.Reset();

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // find any block's that don't have a metadata entry
                    var missingSet = new HashSet<UInt256>(
                        this._storageManager.BlockDataCache.GetAllKeys()
                        .Except(this._storageManager.BlockMetadataCache.GetAllKeys()));

                    this.updateMetadataSet.UnionWith(missingSet);

                    // notify metadata worker of available work
                    if (missingSet.Count > 0)
                        this.metadataWorkerNotifyEvent.Set();

                    stopwatch.Stop();
                    Debug.WriteLine("MissingMetadataWorker: Found {0} items in {1:#,##0.000}s".Format2(missingSet.Count, stopwatch.EllapsedSecondsFloat()));
                }
            }
            catch (OperationCanceledException) { }
        }

        //TODO
        private void MetadataWorker()
        {
            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // wait for work notification
                    this.metadataWorkerIdleEvent.Set();
                    this.metadataWorkerNotifyEvent.WaitOne();

                    // notify that work is starting
                    this.metadataWorkerIdleEvent.Reset();

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // prepare list of work
                    this.updateMetadataSet.UnionWith(this.missingBlockMetadata);
                    var updateMetadataSetLocal = new HashSet<UInt256>(this.updateMetadataSet);
                    var updateMetadataSetSnapshot = new HashSet<UInt256>(updateMetadataSetLocal);

                    // add the metadata entries
                    var totalUpdateCount = 0;
                    var updateCount = 0;
                    while (updateMetadataSetLocal.Count > 0)
                    {
                        // cooperative loop
                        this.shutdownToken.Token.ThrowIfCancellationRequested();

                        var blockHash = updateMetadataSetLocal.First();
                        updateCount += UpdateMetadata(blockHash, updateMetadataSetLocal);
                        totalUpdateCount += updateCount;

                        // periodically notify chaining worker if processing a large update
                        if (updateCount > 10.THOUSAND())
                        {
                            this.chainingWorkerNotifyEvent.Set();
                            updateCount = 0;
                        }
                    }

                    this.updateMetadataSet.ExceptWith(updateMetadataSetSnapshot);

                    if (updateCount > 0)
                    {
                        // notify the chaining and blockchain workers after adding missing metadatda
                        this.chainingWorkerNotifyEvent.Set();
                        this.blockchainWorkerNotifyEvent.Set();
                    }

                    stopwatch.Stop();
                    Debug.WriteLine("MetadataWorker: Updated {0} items items in {1:#,##0.000}s".Format2(totalUpdateCount, stopwatch.EllapsedSecondsFloat()));
                }
            }
            catch (OperationCanceledException) { }
        }

        private int UpdateMetadata(UInt256 blockHash, HashSet<UInt256> workList, int depth = 0)
        {
            var updateCount = 0;
            workList.Remove(blockHash);

            bool isChanged;
            BlockMetadata updatedMetadata;

            if (TryGetBlockMetadata(blockHash, out updatedMetadata))
            {
                isChanged = false;
            }
            else
            {
                BlockHeader blockHeader;
                if (TryGetBlockHeader(blockHash, out blockHeader))
                {
                    isChanged = true;
                    updatedMetadata = new BlockMetadata
                    (
                        blockHash,
                        PreviousBlockHash: blockHeader.PreviousBlock,
                        Work: this.Rules.CalculateWork(blockHeader),
                        Height: null,
                        TotalWork: null,
                        IsValid: null
                    );
                }
                else
                {
                    return updateCount;
                }
            }

            // check if the previous block is missing
            var prevMissing = !this.StorageManager.BlockMetadataCache.ContainsKey(updatedMetadata.PreviousBlockHash);

            // if current block is unchained and the previous block is present, see if current block can be chained from the previous block
            if (updatedMetadata.Height == null && !prevMissing)
            {
                BlockMetadata prevBlockMetadata;
                if (
                    TryGetBlockMetadata(updatedMetadata.PreviousBlockHash, out prevBlockMetadata)
                    && prevBlockMetadata.Height != null && prevBlockMetadata.TotalWork != null)
                {
                    isChanged = true;
                    updatedMetadata = new BlockMetadata
                    (
                            BlockHash: updatedMetadata.BlockHash,
                            PreviousBlockHash: updatedMetadata.PreviousBlockHash,
                            Work: updatedMetadata.Work,
                            Height: prevBlockMetadata.Height + 1,
                            TotalWork: prevBlockMetadata.TotalWork + updatedMetadata.Work,
                            IsValid: null
                    );
                }
                else
                {
                    prevMissing = true;
                }
            }

            // update the metadata in storage if anything changed
            if (isChanged)
            {
                this._storageManager.BlockMetadataCache.UpdateValue(blockHash, updatedMetadata);
                updateCount++;
            }

            // if the previous block is missing or unchained, attempt to update it
            if (prevMissing)
            {
                if (depth < 50)
                {
                    updateCount += UpdateMetadata(updatedMetadata.PreviousBlockHash, workList, depth + 1);
                }
                else
                {
                    updateMetadataSet.TryAdd(updatedMetadata.PreviousBlockHash);
                    this.metadataWorkerNotifyEvent.Set();
                }
            }

            return updateCount;
        }

        private void ChainingWorker()
        {
            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // wait for work notification
                    this.chainingWorkerIdleEvent.Set();
                    this.chainingWorkerNotifyEvent.WaitOne();

                    // notify that work is starting
                    this.chainingWorkerIdleEvent.Reset();

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // grab a snapshot of pending blocks
                    var pendingBlockMetadata =
                        this.StorageManager.BlockMetadataCache.GetPendingValues()
                        .ToDictionary(x => x.Key, x => x.Value);

                    // find any previous blocks that are missing
                    //TODO
                    //var missingPreviousBlocks = this.StorageManager.BlockMetadataStorage.FindMissingPreviousBlocks(this.StorageManager.BlockMetadataCache.GetAllKeys(), pendingBlockMetadata);
                    //this.missingBlocks.UnionWith(missingPreviousBlocks);

                    // find any chained blocks that are followed by unchained blocks
                    var chainedWithProceeding = this._storageManager.BlockMetadataStorage.FindChainedWithProceedingUnchained(pendingBlockMetadata);

                    // check if there are any chainable blocks
                    var chainCount = 0;
                    if (chainedWithProceeding.Count > 0)
                    {
                        // unchained blocks are present, look them  up
                        var unchainedBlocksByPrevious = new MethodTimer().Time("FindUnchainedBlocksByPrevious", () =>
                            this._storageManager.BlockMetadataStorage.FindUnchainedBlocksByPrevious());

                        bool wasLoopAdvanced = true;
                        BlockMetadata? lastChainedBlock = null;
                        while (wasLoopAdvanced)
                        {
                            // cooperative loop
                            this.shutdownToken.Token.ThrowIfCancellationRequested();

                            wasLoopAdvanced = false;

                            // find any unchained blocks that are preceded by a  chained block
                            while (chainedWithProceeding.Count > 0)
                            {
                                // cooperative loop
                                this.shutdownToken.Token.ThrowIfCancellationRequested();

                                // grab an item
                                var chained = chainedWithProceeding.Keys.First();
                                var proceeding = chainedWithProceeding[chained];

                                // remove item to be processed from the list
                                chainedWithProceeding.Remove(chained);

                                foreach (var unchained in proceeding)
                                {
                                    // cooperative loop
                                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                                    var newMetadata = new BlockMetadata
                                    (
                                        BlockHash: unchained.BlockHash,
                                        PreviousBlockHash: unchained.PreviousBlockHash,
                                        Work: unchained.Work,
                                        Height: chained.Height.Value + 1,
                                        TotalWork: chained.TotalWork.Value + unchained.Work,
                                        IsValid: unchained.IsValid
                                    );

                                    this._storageManager.BlockMetadataCache.UpdateValue(newMetadata.BlockHash, newMetadata);

                                    chainCount++;
                                    lastChainedBlock = newMetadata;
                                    wasLoopAdvanced = true;
                                    Debug.WriteLineIf(chainCount % 1.THOUSAND() == 0, "Chained block {0} at height {1}, total work: {2}".Format2(newMetadata.BlockHash.ToHexNumberString(), newMetadata.Height.Value, newMetadata.TotalWork.Value.ToString("X")));

                                    if (chainCount % 1.THOUSAND() == 0)
                                    {
                                        // notify the blockchain worker after chaining blocks
                                        this.blockchainWorkerNotifyEvent.Set();
                                    }

                                    // see if any unchained blocks can chain off this newly added link
                                    if (unchainedBlocksByPrevious.ContainsKey(newMetadata.BlockHash))
                                    {
                                        // lookup the metadata for any blocks that can now chain
                                        var proceeding2 = new HashSet<BlockMetadata>();
                                        foreach (var proceedingHash in unchainedBlocksByPrevious[newMetadata.BlockHash])
                                        {
                                            // cooperative loop
                                            this.shutdownToken.Token.ThrowIfCancellationRequested();

                                            BlockMetadata proceedingMetadata;
                                            if (TryGetBlockMetadata(proceedingHash, out proceedingMetadata))
                                            {
                                                // only keep looking if next block isn't already chained
                                                if (proceedingMetadata.Height == null)
                                                    proceeding2.Add(proceedingMetadata);
                                            }
                                        }

                                        // add the newly chainable blocks to the list to be processed
                                        if (!chainedWithProceeding.ContainsKey(newMetadata))
                                            chainedWithProceeding.Add(newMetadata, new HashSet<BlockMetadata>());
                                        chainedWithProceeding[newMetadata].UnionWith(proceeding2);
                                    }
                                }
                            }
                        }

                        if (lastChainedBlock != null)
                            Debug.WriteLine("Chained block {0} at height {1}, total work: {2}".Format2(lastChainedBlock.Value.BlockHash.ToHexNumberString(), lastChainedBlock.Value.Height.Value, lastChainedBlock.Value.TotalWork.Value.ToString("X")));

                        if (chainCount > 0)
                        {
                            // keep looking for more broken links after each pass
                            this.chainingWorkerNotifyEvent.Set();
                        }

                        // notify the blockchain worker after chaining blocks
                        this.blockchainWorkerNotifyEvent.Set();
                    }

                    stopwatch.Stop();
                    Debug.WriteLine("ChainingWorker: Chained {0:#,##0} items in {1:#,##0.000}s".Format2(chainCount, stopwatch.EllapsedSecondsFloat()));
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ValidationWorker()
        {
            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // wait for work notification
                    this.validationWorkerIdleEvent.Set();
                    this.validationWorkerNotifyEvent.WaitOne();

                    // notify that work is starting
                    this.validationWorkerIdleEvent.Reset();


                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    stopwatch.Stop();
                    Debug.WriteLine("ValidationWorker: {0:#,##0.000}s".Format2(stopwatch.EllapsedSecondsFloat()));
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ValidateCurrentChainWorker()
        {
            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // wait for work notification
                    this.validateCurrentChainWorkerIdleEvent.Set();
                    this.validateCurrentChainWorkerNotifyEvent.WaitOne();

                    // notify that work is starting
                    this.validateCurrentChainWorkerIdleEvent.Reset();

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // revalidate current blockchain
                    try
                    {
                        this.calculator.RevalidateBlockchain(this.currentBlockchain, this._rules.GenesisBlock);
                    }
                    catch (ValidationException e)
                    {
                        //TODO this does not cancel a blockchain that is currently being processed

                        Debug.WriteLine("******************************");
                        Debug.WriteLine("******************************");
                        Debug.WriteLine("BLOCKCHAIN ERROR DETECTED, ROLLING BACK TO GENESIS");
                        Debug.WriteLine("******************************");
                        Debug.WriteLine("******************************");

                        UpdateCurrentBlockchain(this._rules.GenesisBlockchain);
                    }
                    catch (MissingDataException e)
                    {
                        HandleMissingData(e);
                    }

                    stopwatch.Stop();
                    Debug.WriteLine("ValidateCurrentChainWorker: {0:#,##0.000}s".Format2(stopwatch.EllapsedSecondsFloat()));
                }
            }
            catch (OperationCanceledException) { }
        }

        private Stopwatch validateStopwatch = new Stopwatch();
        private void BlockchainWorker()
        {
            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // wait for work notification
                    this.blockchainWorkerIdleEvent.Set();
                    this.blockchainWorkerNotifyEvent.WaitOne();

                    // notify that work is starting
                    this.blockchainWorkerIdleEvent.Reset();

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // grab a snapshot of pending blocks
                    var pendingBlockMetadata =
                        this._storageManager.BlockMetadataCache.GetPendingValues()
                        .ToDictionary(x => x.Key, x => x.Value);

                    // get winning chain metadata from storage, giving flush pending blocks priority
                    var winningChainedBlocks =
                        this._storageManager.BlockMetadataStorage.FindWinningChainedBlocks(pendingBlockMetadata);

                    if (winningChainedBlocks.Count() == 0)
                    {
                        //TODO, error condition
                    }

                    //TODO ordering will need to follow actual bitcoin rules to ensure the same winning chaing is always selected
                    var winningChainedBlock = this._rules.SelectWinningBlockchain(winningChainedBlocks);

                    UpdateWinningBlock(winningChainedBlock);

                    // check if the winning blockchain has changed
                    if (this.currentBlockchain.RootBlockHash != winningChainedBlock.BlockHash)
                    {
                        try
                        {
                            // try to advance the blockchain with the new winning block
                            var newBlockchain = this.calculator.CalculateBlockchainFromExisting(this.currentBlockchain, winningChainedBlock,
                                progressBlockchain => UpdateCurrentBlockchain(progressBlockchain));

                            UpdateCurrentBlockchain(newBlockchain);

                            //TODO
                            // only partially constructed, try to grab the missing data
                            if (newBlockchain.Height < winningChainedBlock.Height)
                            {
                                this.metadataWorkerNotifyEvent.Set();
                                this.chainingWorkerNotifyEvent.Set();

                                //for (var height = 0; height < currentBlockchain.BlockList.Count; height++)
                                //{
                                //    // cooperative loop
                                //    if (this.shutdownThreads)
                                //        return;

                                //    BlockHeader blockHeader;
                                //    if (!TryGetBlockHeader(currentBlockchain.BlockList[height].BlockHash, out blockHeader))
                                //    {
                                //        // rollback until the point before the missing data
                                //        while (currentBlockchain.Height >= height)
                                //        {
                                //            // cooperative loop
                                //            if (this.shutdownThreads)
                                //                return;

                                //            // rollback the current blockchain
                                //            currentBlockchain = this.calculator.RollbackBlockchain(currentBlockchain);
                                //        }
                                //    }
                                //}
                            }

                            // whenever the chain is successfully advanced, keep looking for more
                            this.blockchainWorkerNotifyEvent.Set();

                            // kick off a blockchain revalidate after update
                            this.validateCurrentChainWorkerNotifyEvent.Set();
                        }
                        catch (ValidationException e)
                        {
                            //TODO
                            // an invalid blockchain with winning work will just keep trying over and over again until this is implemented
                        }
                        catch (MissingDataException e)
                        {
                            HandleMissingData(e);
                        }
                    }

                    stopwatch.Stop();
                    Debug.WriteLine("BlockchainWorker: {0:#,##0.000}s".Format2(stopwatch.EllapsedSecondsFloat()));
                }
            }
            catch (OperationCanceledException) { }
        }

        //TODO
        private void LoadExistingState()
        {
        }

        public bool TryGetBlock(UInt256 blockHash, out Block block, bool saveInCache = true)
        {
            if (this._storageManager.BlockDataCache.TryGetValue(blockHash, out block, saveInCache))
            {
                this.missingBlocks.TryRemove(blockHash);
                return true;
            }
            else
            {
                this.missingBlocks.TryAdd(blockHash);
                block = default(Block);
                return false;
            }
        }

        public bool TryGetBlockHeader(UInt256 blockHash, out BlockHeader blockHeader, bool saveInCache = true)
        {
            Block block;
            if (this._storageManager.BlockHeaderCache.TryGetValue(blockHash, out blockHeader, saveInCache))
            {
                this.missingBlocks.TryRemove(blockHash);
                return true;
            }
            else if (this._storageManager.BlockDataCache.TryGetValue(blockHash, out block, saveInCache))
            {
                blockHeader = block.Header;
                this.missingBlocks.TryRemove(blockHash);
                return true;
            }
            else
            {
                this.missingBlocks.TryAdd(blockHash);
                blockHeader = default(BlockHeader);
                return false;
            }
        }

        public bool TryGetBlockMetadata(UInt256 blockHash, out BlockMetadata blockMetadata, bool saveInCache = true)
        {
            if (this._storageManager.BlockMetadataCache.TryGetValue(blockHash, out blockMetadata, saveInCache))
            {
                this.missingBlockMetadata.TryRemove(blockHash);
                return true;
            }
            else
            {
                this.missingBlockMetadata.TryAdd(blockHash);
                if (!this.StorageManager.BlockDataCache.ContainsKey(blockHash))
                    this.missingBlocks.TryAdd(blockHash);

                blockMetadata = default(BlockMetadata);
                return false;
            }
        }

        public bool TryGetTransaction(UInt256 transactionHash, out Transaction transaction, bool saveInCache = true)
        {
            if (this.StorageManager.TransactionStorage.TryReadValue(transactionHash, out transaction))
            {
                return true;
            }
            else
            {
                this.missingTransactions.TryAdd(transactionHash);
                return false;
            }
        }

        public long BlockCacheMemorySize
        {
            get { return this.StorageManager.BlockDataCache.MaxCacheMemorySize; }
        }

        public long HeaderCacheMemorySize
        {
            get { return this.StorageManager.BlockHeaderCache.MaxCacheMemorySize; }
        }

        public long MetadataCacheMemorySize
        {
            get { return this.StorageManager.BlockMetadataCache.MaxCacheMemorySize; }
        }

        private void HandleMissingData(MissingDataException e)
        {
            switch (e.DataType)
            {
                case DataType.Block:
                case DataType.BlockHeader:
                    this.missingBlocks.TryAdd(e.DataKey);
                    break;

                case DataType.BlockMetadata:
                    this.missingBlockMetadata.TryAdd(e.DataKey);
                    break;

                case DataType.Transaction:
                    this.missingTransactions.TryAdd(e.DataKey);
                    break;
            }
        }

        private void UpdateWinningBlock(BlockMetadata winningBlock)
        {
            this.winningBlock = winningBlock;

            var handler = this.OnWinningBlockChanged;
            if (handler != null)
                handler(this, winningBlock);
        }

        private void UpdateCurrentBlockchain(Blockchain.Blockchain newBlockchain)
        {
            this.currentBlockchain = newBlockchain;

            var handler = this.OnCurrentBlockchainChanged;
            if (handler != null)
                handler(this, newBlockchain);
        }
    }
}
