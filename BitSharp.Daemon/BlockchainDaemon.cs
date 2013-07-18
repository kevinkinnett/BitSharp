using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain;
using BitSharp.Storage.Firebird;
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
using BitSharp.Data;

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
    public class BlockchainDaemon : IDisposable
    {
        public event EventHandler<BlockMetadata> OnWinningBlockChanged;
        public event EventHandler<Data.Blockchain> OnCurrentBlockchainChanged;

        private readonly CacheContext _cacheContext;

        private readonly IBlockchainRules _rules;
        private readonly BlockchainCalculator _calculator;

        private BlockMetadata _winningBlock;
        private ImmutableArray<BlockMetadata> _winningBlockchain;

        private Data.Blockchain _currentBlockchain;
        private ReaderWriterLock currentBlockchainLock;
        //TODO
        private Guid lastCurrentBlockchainWrite;

        private readonly ConcurrentSet<UInt256> missingBlocks;
        private readonly ConcurrentSet<UInt256> missingBlockMetadata;
        private readonly ConcurrentSet<UInt256> missingTransactions;
        private readonly ConcurrentSet<UInt256> updateMetadataSet;

        private readonly CancellationTokenSource shutdownToken;

        private readonly Worker missingMetadataWorker;
        private readonly Worker updateMetadataWorker;
        private readonly Worker chainingWorker;
        private readonly Worker winnerWorker;
        private readonly Worker validationWorker;
        private readonly Worker blockchainWorker;
        private readonly Worker validateCurrentChainWorker;
        private readonly Worker writeBlockchainWorker;

        public BlockchainDaemon(IBlockchainRules rules, CacheContext cacheContext)
        {
            this.shutdownToken = new CancellationTokenSource();

            this._rules = rules;
            this._cacheContext = cacheContext;
            this._calculator = new BlockchainCalculator(this._rules, this._cacheContext, this.shutdownToken.Token);

            this._winningBlock = this._rules.GenesisBlockMetadata;
            this._currentBlockchain = this._rules.GenesisBlockchain;
            this.currentBlockchainLock = new ReaderWriterLock();
            //TODO
            this.lastCurrentBlockchainWrite = Guid.NewGuid();

            this.missingBlocks = new ConcurrentSet<UInt256>();
            this.missingBlockMetadata = new ConcurrentSet<UInt256>();
            this.missingTransactions = new ConcurrentSet<UInt256>();
            this.updateMetadataSet = new ConcurrentSet<UInt256>();

            // write genesis block out to storage
            this._cacheContext.BlockCache.UpdateValue(this._rules.GenesisBlock.Hash, this._rules.GenesisBlock);
            this._cacheContext.BlockMetadataCache.UpdateValue(this._rules.GenesisBlockMetadata.BlockHash, this._rules.GenesisBlockMetadata);

            // wait for genesis block to be flushed
            this._cacheContext.BlockCache.WaitForStorageFlush();
            this._cacheContext.BlockMetadataCache.WaitForStorageFlush();

            // pre-fill the metadata cache
            this._cacheContext.BlockMetadataCache.FillCache();

            // wire up cache events
            this._cacheContext.BlockCache.OnAddition += OnBlockDataAddition;
            this._cacheContext.BlockCache.OnModification += OnBlockDataModification;
            this._cacheContext.BlockMetadataCache.OnAddition += OnBlockMetadataAddition;
            this._cacheContext.BlockMetadataCache.OnModification += OnBlockMetadataModification;

            // create workers
            this.missingMetadataWorker = new Worker("MissingMetadataWorker", MissingMetadataWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.updateMetadataWorker = new Worker("UpdateMetadataWorker", UpdateMetadataWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.chainingWorker = new Worker("ChainingWorker", ChainingWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.winnerWorker = new Worker("WinnerWorker", WinnerWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.validationWorker = new Worker("ValidationWorker", ValidationWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(10), maxIdleTime: TimeSpan.FromMinutes(5));

            this.blockchainWorker = new Worker("BlockchainWorker", BlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.validateCurrentChainWorker = new Worker("ValidateCurrentChainWorker", ValidateCurrentChainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(30), maxIdleTime: TimeSpan.FromMinutes(30));

            this.writeBlockchainWorker = new Worker("WriteBlockchainWorker", WriteBlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(1), maxIdleTime: TimeSpan.FromMinutes(30));
        }

        public IBlockchainRules Rules { get { return this._rules; } }

        public BlockchainCalculator Calculator { get { return this._calculator; } }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public BlockMetadata WinningBlock { get { return this._winningBlock; } }

        public ImmutableArray<BlockMetadata> WinningBlockchain { get { return this._winningBlockchain; } }

        public Data.Blockchain CurrentBlockchain { get { return this._currentBlockchain; } }

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
                // start loading the existing state from storage
                LoadExistingState();

                // startup workers
                this.missingMetadataWorker.Start();
                this.updateMetadataWorker.Start();
                this.chainingWorker.Start();
                this.winnerWorker.Start();
                this.validationWorker.Start();
                this.blockchainWorker.Start();
                this.validateCurrentChainWorker.Start();
                this.writeBlockchainWorker.Start();
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
            this.CacheContext.BlockCache.OnAddition -= OnBlockDataAddition;
            this.CacheContext.BlockCache.OnModification -= OnBlockDataModification;
            this.CacheContext.BlockMetadataCache.OnAddition -= OnBlockMetadataAddition;
            this.CacheContext.BlockMetadataCache.OnModification -= OnBlockMetadataModification;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.missingMetadataWorker,
                this.updateMetadataWorker,
                this.chainingWorker,
                this.winnerWorker,
                this.validationWorker,
                this.blockchainWorker,
                this.validateCurrentChainWorker,
                this.writeBlockchainWorker,
                this.shutdownToken
            }.DisposeList();
        }

        public void WaitForFullUpdate()
        {
            WaitForMissingMetadataUpdate();
            WaitForUpdateMetadataUpdate();
            WaitForChainingUpdate();
            WaitForWinnerUpdate();
            WaitForBlockchainUpdate();
        }

        public void WaitForMissingMetadataUpdate()
        {
            this.missingMetadataWorker.ForceWorkAndWait();
        }

        public void WaitForUpdateMetadataUpdate()
        {
            this.updateMetadataWorker.ForceWorkAndWait();
        }

        public void WaitForChainingUpdate()
        {
            this.chainingWorker.ForceWorkAndWait();
        }

        public void WaitForWinnerUpdate()
        {
            this.winnerWorker.ForceWorkAndWait();
        }

        public void WaitForBlockchainUpdate()
        {
            this.blockchainWorker.ForceWorkAndWait();
        }

        private void OnBlockDataAddition(UInt256 blockHash)
        {
            OnBlockDataModification(blockHash);
        }

        private void OnBlockDataModification(UInt256 blockHash)
        {
            this.updateMetadataSet.TryAdd(blockHash);

            if (this.missingBlocks.TryRemove(blockHash))
            {
                this.updateMetadataWorker.ForceWork();
                this.chainingWorker.NotifyWork();
                this.blockchainWorker.NotifyWork();
            }
            else
            {
                this.updateMetadataWorker.NotifyWork();
                this.chainingWorker.NotifyWork();
                this.blockchainWorker.NotifyWork();
            }
        }

        private void OnBlockMetadataAddition(UInt256 blockHash)
        {
            OnBlockMetadataModification(blockHash);
        }

        private void OnBlockMetadataModification(UInt256 blockHash)
        {
            this.updateMetadataSet.TryAdd(blockHash);

            if (this.missingBlockMetadata.TryRemove(blockHash))
            {
                this.updateMetadataWorker.ForceWork();
                //this.chainingWorker.ForceWork();
                //this.blockchainWorker.ForceWork();
            }
            else
            {
                this.updateMetadataWorker.NotifyWork();
                //this.chainingWorker.NotifyWork();
                //this.blockchainWorker.NotifyWork();
            }
        }

        private void LoadExistingState()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            //TODO
            Tuple<BlockchainKey, BlockchainMetadata> winner = null;

            foreach (var tuple in this.StorageContext.BlockchainStorage.ListBlockchains())
            {
                if (winner == null)
                    winner = tuple;

                if (tuple.Item2.TotalWork > winner.Item2.TotalWork)
                {
                    winner = tuple;
                }
            }

            // check if an existing blockchain has been found
            if (winner != null)
            {
                // read the winning blockchain
                var blockchain = this.StorageContext.BlockchainStorage.ReadBlockchain(winner.Item1);
                UpdateCurrentBlockchain(blockchain);
                UpdateWinningBlock(blockchain.RootBlock, blockchain.BlockList.ToImmutableArray());

                // collect after loading
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

                // clean up any old blockchains
                this.StorageContext.BlockchainStorage.RemoveBlockchains(winner.Item2.TotalWork);

                // log statistics
                stopwatch.Stop();
                Debug.WriteLine(
                    string.Join("\n",
                        new string('-', 80),
                        "Loaded blockchain on startup in {0:#,##0.000} seconds, height: {1:#,##0}, utxo size: {2:#,##0}",
                        "GC Memory:      {3,10:#,##0.00} MB",
                        "Process Memory: {4,10:#,##0.00} MB",
                        new string('-', 80)
                    )
                    .Format2
                    (
                        stopwatch.EllapsedSecondsFloat(),
                        blockchain.Height,
                        blockchain.Utxo.Count,
                        (float)GC.GetTotalMemory(false) / 1.MILLION(),
                        (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()
                    ));
            }
        }

        private void MissingMetadataWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // find any blocks with a missing metadata record
            var missingSet = new HashSet<UInt256>(
                this.CacheContext.BlockCache.GetAllKeys()
                .Except(this.CacheContext.BlockMetadataCache.GetAllKeys()));

            // notify metadata worker of available work
            if (missingSet.Count > 0)
            {
                this.updateMetadataSet.UnionWith(missingSet);
                this.updateMetadataWorker.NotifyWork();
            }

            // find any metadata with a missing block record
            foreach (var blockHash in CacheContext.BlockMetadataCache.FindMissingBlocks())
            {
                this.missingBlocks.TryAdd(blockHash);
            }

            // find any previous blocks that are missing
            var missingPreviousBlocks = this.CacheContext.BlockMetadataCache.FindMissingPreviousBlocks();
            this.missingBlocks.UnionWith(missingPreviousBlocks);

            stopwatch.Stop();
            Debug.WriteLine("MissingMetadataWorker: {0:#,##0.000}s".Format2(stopwatch.EllapsedSecondsFloat()));
        }

        private void UpdateMetadataWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // prepare list of work
            this.updateMetadataSet.UnionWith(this.missingBlockMetadata);
            var updateMetadataSetLocal = new HashSet<UInt256>(this.updateMetadataSet);
            var updateMetadataSetSnapshot = new HashSet<UInt256>(updateMetadataSetLocal);

            // add the metadata entries
            var totalUpdateCount = 0;
            var currentUpdateCount = 0;
            while (updateMetadataSetLocal.Count > 0)
            {
                // cooperative loop
                this.shutdownToken.Token.ThrowIfCancellationRequested();

                var blockHash = updateMetadataSetLocal.First();
                var updateCount = UpdateMetadata(blockHash, updateMetadataSetLocal);
                currentUpdateCount += updateCount;
                totalUpdateCount += updateCount;

                // periodically notify chaining worker if processing a large update
                if (currentUpdateCount > 10.THOUSAND())
                {
                    this.chainingWorker.NotifyWork();
                    currentUpdateCount = 0;
                }
            }

            this.updateMetadataSet.ExceptWith(updateMetadataSetSnapshot);

            if (totalUpdateCount > 0)
            {
                // notify the chaining, winner and blockchain workers after adding missing metadatda
                this.chainingWorker.NotifyWork();
                this.winnerWorker.NotifyWork();
                this.blockchainWorker.NotifyWork();
            }

            stopwatch.Stop();
            //Debug.WriteLine("UpdateMetadataWorker: Updated {0} items items in {1:#,##0.000}s".Format2(totalUpdateCount, stopwatch.EllapsedSecondsFloat()));
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
                        previousBlockHash: blockHeader.PreviousBlock,
                        work: blockHeader.CalculateWork(),
                        height: null,
                        totalWork: null,
                        isValid: null
                    );
                }
                else
                {
                    return updateCount;
                }
            }

            // check if the previous block is missing
            var prevMissing = !this.CacheContext.BlockMetadataCache.ContainsKey(updatedMetadata.PreviousBlockHash);

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
                            blockHash: updatedMetadata.BlockHash,
                            previousBlockHash: updatedMetadata.PreviousBlockHash,
                            work: updatedMetadata.Work,
                            height: prevBlockMetadata.Height + 1,
                            totalWork: prevBlockMetadata.TotalWork + updatedMetadata.Work,
                            isValid: null
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
                this.CacheContext.BlockMetadataCache.UpdateValue(blockHash, updatedMetadata);
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
                    this.updateMetadataWorker.NotifyWork();
                }
            }

            return updateCount;
        }

        private void ChainingWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // find any chained blocks that are followed by unchained blocks
            var chainedWithProceeding = this.CacheContext.BlockMetadataCache.FindChainedWithProceedingUnchained();

            // check if there are any chainable blocks
            var chainCount = 0;
            if (chainedWithProceeding.Count > 0)
            {
                // unchained blocks are present, look them  up
                var unchainedBlocksByPrevious = new MethodTimer().Time("FindUnchainedBlocksByPrevious", () =>
                    this.CacheContext.BlockMetadataCache.FindUnchainedBlocksByPrevious());

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
                                blockHash: unchained.BlockHash,
                                previousBlockHash: unchained.PreviousBlockHash,
                                work: unchained.Work,
                                height: chained.Height.Value + 1,
                                totalWork: chained.TotalWork.Value + unchained.Work,
                                isValid: unchained.IsValid
                            );

                            this.CacheContext.BlockMetadataCache.UpdateValue(newMetadata.BlockHash, newMetadata);

                            chainCount++;
                            lastChainedBlock = newMetadata;
                            wasLoopAdvanced = true;
                            Debug.WriteLineIf(chainCount % 1.THOUSAND() == 0, "Chained block {0} at height {1}, total work: {2}".Format2(newMetadata.BlockHash.ToHexNumberString(), newMetadata.Height.Value, newMetadata.TotalWork.Value.ToString("X")));

                            if (chainCount % 1.THOUSAND() == 0)
                            {
                                // notify the blockchain worker after chaining blocks
                                this.blockchainWorker.NotifyWork();
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
                    this.chainingWorker.NotifyWork();
                }

                // notify winner worker after chaining blocks
                this.winnerWorker.NotifyWork();

                // notify the blockchain worker after chaining blocks
                this.blockchainWorker.NotifyWork();
            }

            stopwatch.Stop();
            //Debug.WriteLine("ChainingWorker: Chained {0:#,##0} items in {1:#,##0.000}s".Format2(chainCount, stopwatch.EllapsedSecondsFloat()));
        }

        private void WinnerWorker()
        {
            try
            {
                // get winning chain metadata
                var winningChainedBlocks = this.CacheContext.BlockMetadataCache.FindWinningChainedBlocks();

                //TODO ordering will need to follow actual bitcoin rules to ensure the same winning chaing is always selected
                var winningBlock = this._rules.SelectWinningBlockchain(winningChainedBlocks);

                if (winningBlock.BlockHash != this.WinningBlock.BlockHash)
                {
                    var winningBlockchain = new List<BlockMetadata>();
                    foreach (var winningLink in Calculator.PreviousBlockMetadata(winningBlock))
                    {
                        winningBlockchain.Add(winningLink);
                    }
                    winningBlockchain.Reverse();

                    UpdateWinningBlock(winningBlock, winningBlockchain.ToImmutableArray());
                }
            }
            catch (MissingDataException e)
            {
                HandleMissingData(e);
            }
            catch (AggregateException e)
            {
                foreach (var missingDataException in e.InnerExceptions.OfType<MissingDataException>())
                {
                    HandleMissingData(missingDataException);
                }

                if (e.InnerExceptions.Any(x => !(x is MissingDataException)))
                    throw;
            }
        }

        private void ValidationWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            stopwatch.Stop();
            Debug.WriteLine("ValidationWorker: {0:#,##0.000}s".Format2(stopwatch.EllapsedSecondsFloat()));
        }

        private void ValidateCurrentChainWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // revalidate current blockchain
            try
            {
                Calculator.RevalidateBlockchain(this._currentBlockchain, this._rules.GenesisBlock);
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

        private Stopwatch validateStopwatch = new Stopwatch();
        private void BlockchainWorker()
        {
            try
            {
                var winningBlockLocal = this.WinningBlock;

                // check if the winning blockchain has changed
                if (this._currentBlockchain.RootBlockHash != winningBlockLocal.BlockHash)
                {
                    var lastCurrentBlockchainWriteLocal = this.lastCurrentBlockchainWrite;
                    using (var cancelToken = new CancellationTokenSource())
                    {
                        //TODO cleanup this design
                        List<MissingDataException> missingData;

                        // try to advance the blockchain with the new winning block
                        var newBlockchain = Calculator.CalculateBlockchainFromExisting(this._currentBlockchain, winningBlockLocal, out missingData, cancelToken.Token,
                            progressBlockchain =>
                            {
                                // check that nothing else has changed the current blockchain
                                currentBlockchainLock.DoRead(() =>
                                {
                                    if (lastCurrentBlockchainWriteLocal != this.lastCurrentBlockchainWrite)
                                    {
                                        cancelToken.Cancel();
                                        return;
                                    }
                                });

                                // update the current blockchain
                                lastCurrentBlockchainWriteLocal = UpdateCurrentBlockchain(progressBlockchain);

                                // let the blockchain writer know there is new work
                                this.writeBlockchainWorker.NotifyWork();
                            });

                        // collect after processing
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

                        // handle any missing data that prevented further processing
                        foreach (var e in missingData)
                        {
                            HandleMissingData(e);
                        }
                    }

                    // whenever the chain is successfully advanced, keep looking for more
                    this.blockchainWorker.NotifyWork();

                    // kick off a blockchain revalidate after update
                    this.validateCurrentChainWorker.NotifyWork();
                }
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
            catch (AggregateException e)
            {
                foreach (var missingDataException in e.InnerExceptions.OfType<MissingDataException>())
                {
                    HandleMissingData(missingDataException);
                }

                //TODO
                //var validationException = e.InnerExceptions.FirstOrDefault(x => x is ValidationException);
                //if (validationException != null)
                //    throw validationException;

                //TODO
                //throw;
            }
        }

        private void WriteBlockchainWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // grab a snapshot
            var currentBlockchainLocal = this._currentBlockchain;

            // don't write out genesis blockchain
            if (currentBlockchainLocal.Height > 0)
            {
                //TODO
                this.StorageContext.BlockchainStorage.WriteBlockchain(currentBlockchainLocal);
                this.StorageContext.BlockchainStorage.RemoveBlockchains(currentBlockchainLocal.TotalWork);
            }

            stopwatch.Stop();
            Debug.WriteLine("WriteBlockchainWorker: {0:#,##0.000}s".Format2(stopwatch.EllapsedSecondsFloat()));
        }

        public bool TryGetBlock(UInt256 blockHash, out Block block, bool saveInCache = true)
        {
            if (this.CacheContext.BlockCache.TryGetValue(blockHash, out block, saveInCache))
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
            if (this.CacheContext.BlockHeaderCache.TryGetValue(blockHash, out blockHeader, saveInCache))
            {
                this.missingBlocks.TryRemove(blockHash);
                return true;
            }
            else if (this.CacheContext.BlockCache.TryGetValue(blockHash, out block, saveInCache))
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
            if (this.CacheContext.BlockMetadataCache.TryGetValue(blockHash, out blockMetadata, saveInCache))
            {
                this.missingBlockMetadata.TryRemove(blockHash);
                return true;
            }
            else
            {
                this.missingBlockMetadata.TryAdd(blockHash);
                if (!this.CacheContext.BlockCache.ContainsKey(blockHash))
                    this.missingBlocks.TryAdd(blockHash);

                blockMetadata = default(BlockMetadata);
                return false;
            }
        }

        public bool TryGetTransaction(TxKeySearch txKeySearch, out Transaction transaction, bool saveInCache = true)
        {
            if (this.CacheContext.TransactionCache.TryGetValue(txKeySearch, out transaction))
            {
                this.missingTransactions.TryRemove(txKeySearch.TxHash);
                return true;
            }
            else
            {
                this.missingTransactions.TryAdd(txKeySearch.TxHash);
                transaction = default(Transaction);
                return false;
            }
        }

        public long BlockCacheMemorySize
        {
            get { return this.CacheContext.BlockCache.MaxCacheMemorySize; }
        }

        public long HeaderCacheMemorySize
        {
            get { return this.CacheContext.BlockHeaderCache.MaxCacheMemorySize; }
        }

        public long MetadataCacheMemorySize
        {
            get { return this.CacheContext.BlockMetadataCache.MaxCacheMemorySize; }
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

        private void UpdateWinningBlock(BlockMetadata winningBlock, ImmutableArray<BlockMetadata> winningBlockchain)
        {
            this._winningBlock = winningBlock;
            this._winningBlockchain = winningBlockchain;

            var handler = this.OnWinningBlockChanged;
            if (handler != null)
                handler(this, winningBlock);
        }

        private Guid UpdateCurrentBlockchain(Data.Blockchain newBlockchain)
        {
            var guid = Guid.NewGuid();

            this.currentBlockchainLock.DoWrite(() =>
            {
                this.lastCurrentBlockchainWrite = guid;
                this._currentBlockchain = newBlockchain;
            });

            var handler = this.OnCurrentBlockchainChanged;
            if (handler != null)
                handler(this, newBlockchain);

            return guid;
        }
    }
}
