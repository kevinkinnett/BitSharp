using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain;
using BitSharp.Storage.Firebird;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Storage.ExtensionMethods;
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
        public event EventHandler<ChainedBlock> OnWinningBlockChanged;
        public event EventHandler<Data.Blockchain> OnCurrentBlockchainChanged;

        private readonly CacheContext _cacheContext;

        private readonly IBlockchainRules _rules;
        private readonly BlockchainCalculator _calculator;

        private ChainedBlock _winningBlock;
        private ImmutableArray<ChainedBlock> _winningBlockchain;

        private Data.Blockchain _currentBlockchain;
        private ReaderWriterLockSlim currentBlockchainLock;
        //TODO
        private Guid lastCurrentBlockchainWrite;

        private readonly ConcurrentSet<UInt256> missingBlocks;
        private readonly ConcurrentSet<UInt256> missingChainedBlocks;
        private readonly ConcurrentSet<UInt256> missingTransactions;

        private readonly CancellationTokenSource shutdownToken;

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

            this._winningBlock = this._rules.GenesisChainedBlock;
            this._currentBlockchain = this._rules.GenesisBlockchain;
            this.currentBlockchainLock = new ReaderWriterLockSlim();
            //TODO
            this.lastCurrentBlockchainWrite = Guid.NewGuid();

            this.missingBlocks = new ConcurrentSet<UInt256>();
            this.missingChainedBlocks = new ConcurrentSet<UInt256>();
            this.missingTransactions = new ConcurrentSet<UInt256>();

            // write genesis block out to storage
            this._cacheContext.BlockCache.UpdateValue(this._rules.GenesisBlock.Hash, this._rules.GenesisBlock);
            this._cacheContext.ChainedBlockCache.UpdateValue(this._rules.GenesisChainedBlock.BlockHash, this._rules.GenesisChainedBlock);

            // wait for genesis block to be flushed
            this._cacheContext.BlockCache.WaitForStorageFlush();
            this._cacheContext.ChainedBlockCache.WaitForStorageFlush();

            // pre-fill the chained block and header caches
            //this._cacheContext.BlockHeaderCache.FillCache();
            this._cacheContext.ChainedBlockCache.FillCache();

            // wire up cache events
            this._cacheContext.BlockCache.OnAddition += OnBlockAddition;
            this._cacheContext.BlockCache.OnModification += OnBlockModification;
            this._cacheContext.ChainedBlockCache.OnAddition += OnChainedBlockAddition;
            this._cacheContext.ChainedBlockCache.OnModification += OnChainedBlockModification;

            // create workers
            this.chainingWorker = new Worker("ChainingWorker", ChainingWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromSeconds(30));

            this.winnerWorker = new Worker("WinnerWorker", WinnerWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromSeconds(30));

            this.validationWorker = new Worker("ValidationWorker", ValidationWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(10), maxIdleTime: TimeSpan.FromMinutes(5));

            this.blockchainWorker = new Worker("BlockchainWorker", BlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.validateCurrentChainWorker = new Worker("ValidateCurrentChainWorker", ValidateCurrentChainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(30), maxIdleTime: TimeSpan.FromMinutes(30));

            this.writeBlockchainWorker = new Worker("WriteBlockchainWorker", WriteBlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(5), maxIdleTime: TimeSpan.FromMinutes(30));
        }

        public IBlockchainRules Rules { get { return this._rules; } }

        public BlockchainCalculator Calculator { get { return this._calculator; } }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public ChainedBlock WinningBlock { get { return this._winningBlock; } }

        public ImmutableArray<ChainedBlock> WinningBlockchain { get { return this._winningBlockchain; } }

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
            this.CacheContext.BlockCache.OnAddition -= OnBlockAddition;
            this.CacheContext.BlockCache.OnModification -= OnBlockModification;
            this.CacheContext.ChainedBlockCache.OnAddition -= OnChainedBlockAddition;
            this.CacheContext.ChainedBlockCache.OnModification -= OnChainedBlockModification;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
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
            WaitForChainingUpdate();
            WaitForWinnerUpdate();
            WaitForBlockchainUpdate();
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

        private void OnBlockAddition(UInt256 blockHash)
        {
            if (this.missingBlocks.TryRemove(blockHash))
            {
                this.chainingWorker.NotifyWork();
                this.blockchainWorker.NotifyWork();
            }
            else
            {
                this.chainingWorker.NotifyWork();
                this.blockchainWorker.NotifyWork();
            }
        }

        private void OnBlockModification(UInt256 blockHash, Block block)
        {
            OnBlockAddition(blockHash);
        }

        private void OnChainedBlockAddition(UInt256 blockHash)
        {
            if (this.missingChainedBlocks.TryRemove(blockHash))
            {
                this.chainingWorker.NotifyWork();
                //this.blockchainWorker.ForceWork();
            }
            else
            {
                this.chainingWorker.NotifyWork();
                //this.blockchainWorker.NotifyWork();
            }
        }

        private void OnChainedBlockModification(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            OnChainedBlockAddition(blockHash);
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
                        stopwatch.ElapsedSecondsFloat(),
                        blockchain.Height,
                        blockchain.Utxo.Count,
                        (float)GC.GetTotalMemory(false) / 1.MILLION(),
                        (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()
                    ));
            }
        }

        private void ChainingWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var chainCount = 0;
            ChainedBlock? lastChainedBlock = null;

            var chainedBlocks = new List<ChainedBlock>();
            var chainedBlocksSet = new HashSet<UInt256>();

            var unchainedBlocks = new HashSet<UInt256>(this.CacheContext.BlockCache.GetAllKeys());
            unchainedBlocks.ExceptWith(this.CacheContext.ChainedBlockCache.GetAllKeys());

            var unchainedByPrevious = new Dictionary<UInt256, List<BlockHeader>>();
            foreach (var unchainedBlock in unchainedBlocks.ToList())
            {
                BlockHeader unchainedBlockHeader;
                if (this.CacheContext.BlockHeaderCache.TryGetValue(unchainedBlock, out unchainedBlockHeader))
                {
                    if (!chainedBlocksSet.Contains(unchainedBlockHeader.PreviousBlock))
                    {
                        ChainedBlock chainedBlock;
                        if (this.CacheContext.ChainedBlockCache.ContainsKey(unchainedBlockHeader.PreviousBlock)
                            && this.CacheContext.ChainedBlockCache.TryGetValue(unchainedBlockHeader.PreviousBlock, out chainedBlock))
                        {
                            chainedBlocks.Add(chainedBlock);
                            chainedBlocksSet.Add(chainedBlock.BlockHash);
                        }
                    }

                    List<BlockHeader> unchainedGroup;
                    if (!unchainedByPrevious.TryGetValue(unchainedBlockHeader.PreviousBlock, out unchainedGroup))
                    {
                        unchainedGroup = new List<BlockHeader>();
                        unchainedByPrevious.Add(unchainedBlockHeader.PreviousBlock, unchainedGroup);
                    }
                    unchainedGroup.Add(unchainedBlockHeader);
                }
                else
                {
                    this.missingBlocks.TryAdd(unchainedBlock);
                }
            }

            // start with chained blocks...
            for (var i = 0; i < chainedBlocks.Count; i++)
            {
                // cooperative loop
                this.shutdownToken.Token.ThrowIfCancellationRequested();

                var chainedBlock = chainedBlocks[i];

                // find any unchained blocks whose previous block is the current chained block...
                IList<BlockHeader> unchainedGroup;
                if (unchainedByPrevious.ContainsKey(chainedBlock.BlockHash))
                {
                    unchainedGroup = unchainedByPrevious[chainedBlock.BlockHash];
                    unchainedByPrevious.Remove(chainedBlock.BlockHash);
                }
                else
                {
                    unchainedGroup = new List<BlockHeader>();
                    foreach (var blockHash in this.CacheContext.ChainedBlockCache.FindByPreviousBlockHash(chainedBlock.BlockHash))
                    {
                        BlockHeader blockHeader;
                        if (this.CacheContext.BlockHeaderCache.TryGetValue(blockHash, out blockHeader))
                            unchainedGroup.Add(blockHeader);
                    }
                }

                foreach (var unchainedBlock in unchainedGroup)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // check that block hasn't become chained
                    if (this.CacheContext.ChainedBlockCache.ContainsKey(unchainedBlock.Hash))
                        break;

                    // update the unchained block to chain off of the current chained block...
                    var newChainedBlock = new ChainedBlock
                    (
                        unchainedBlock.Hash,
                        unchainedBlock.PreviousBlock,
                        chainedBlock.Height + 1,
                        chainedBlock.TotalWork + unchainedBlock.CalculateWork()
                    );
                    this.CacheContext.ChainedBlockCache.CreateValue(newChainedBlock.BlockHash, newChainedBlock);

                    // and finally add the newly chained block to the list of chained blocks so that an attempt will be made to chain off of it
                    chainedBlocks.Add(newChainedBlock);

                    // statistics
                    chainCount++;
                    lastChainedBlock = newChainedBlock;
                    Debug.WriteLineIf(chainCount % 1.THOUSAND() == 0, "Chained block {0} at height {1}, total work: {2}".Format2(newChainedBlock.BlockHash.ToHexNumberString(), newChainedBlock.Height, newChainedBlock.TotalWork.ToString("X")));

                    if (chainCount % 1.THOUSAND() == 0)
                    {
                        // notify winner worker after chaining blocks
                        this.winnerWorker.NotifyWork();

                        // notify the blockchain worker after chaining blocks
                        this.blockchainWorker.NotifyWork();
                    }
                }
            }

            if (lastChainedBlock != null)
                Debug.WriteLine("Chained block {0} at height {1}, total work: {2}".Format2(lastChainedBlock.Value.BlockHash.ToHexNumberString(), lastChainedBlock.Value.Height, lastChainedBlock.Value.TotalWork.ToString("X")));

            if (chainCount > 0)
            {
                // keep looking for more broken links after each pass
                this.chainingWorker.NotifyWork();
            }

            // notify winner worker after chaining blocks
            this.winnerWorker.NotifyWork();

            // notify the blockchain worker after chaining blocks
            this.blockchainWorker.NotifyWork();

            stopwatch.Stop();
            //Debug.WriteLine("ChainingWorker: Chained {0:#,##0} items in {1:#,##0.000}s".Format2(chainCount, stopwatch.ElapsedSecondsFloat()));
        }

        private void WinnerWorker()
        {
            try
            {
                // get winning chain metadata
                var leafChainedBlocks = this.CacheContext.ChainedBlockCache.FindLeafChainedBlocks().ToList();

                //TODO ordering will need to follow actual bitcoin rules to ensure the same winning chaing is always selected
                var winningBlock = this._rules.SelectWinningChainedBlock(leafChainedBlocks);

                List<ChainedBlock> winningChain;
                if (!winningBlock.IsDefault
                    && winningBlock.BlockHash != this.WinningBlock.BlockHash
                    && this.CacheContext.ChainedBlockCache.TryGetChain(winningBlock, out winningChain))
                {
                    UpdateWinningBlock(winningBlock, winningChain.ToImmutableArray());
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
            Debug.WriteLine("ValidationWorker: {0:#,##0.000}s".Format2(stopwatch.ElapsedSecondsFloat()));
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
            Debug.WriteLine("ValidateCurrentChainWorker: {0:#,##0.000}s".Format2(stopwatch.ElapsedSecondsFloat()));
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
            Debug.WriteLine("WriteBlockchainWorker: {0:#,##0.000}s".Format2(stopwatch.ElapsedSecondsFloat()));
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

        public bool TryGetChainedBlock(UInt256 blockHash, out ChainedBlock chainedBlock, bool saveInCache = true)
        {
            if (this.CacheContext.ChainedBlockCache.TryGetValue(blockHash, out chainedBlock, saveInCache))
            {
                this.missingChainedBlocks.TryRemove(blockHash);
                return true;
            }
            else
            {
                this.missingChainedBlocks.TryAdd(blockHash);
                if (!this.CacheContext.BlockCache.ContainsKey(blockHash))
                    this.missingBlocks.TryAdd(blockHash);

                chainedBlock = default(ChainedBlock);
                return false;
            }
        }

        public bool TryGetTransaction(UInt256 txHash, out Transaction transaction, bool saveInCache = true)
        {
            if (this.CacheContext.TransactionCache.TryGetValue(txHash, out transaction))
            {
                this.missingTransactions.TryRemove(txHash);
                return true;
            }
            else
            {
                this.missingTransactions.TryAdd(txHash);
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

        public long ChainedBlockCacheMemorySize
        {
            get { return this.CacheContext.ChainedBlockCache.MaxCacheMemorySize; }
        }

        private void HandleMissingData(MissingDataException e)
        {
            switch (e.DataType)
            {
                case DataType.Block:
                case DataType.BlockHeader:
                    this.missingBlocks.TryAdd(e.DataKey);
                    break;

                case DataType.ChainedBlock:
                    this.missingChainedBlocks.TryAdd(e.DataKey);
                    break;

                case DataType.Transaction:
                    this.missingTransactions.TryAdd(e.DataKey);
                    break;
            }
        }

        private void UpdateWinningBlock(ChainedBlock winningBlock, ImmutableArray<ChainedBlock> winningBlockchain)
        {
            this._winningBlock = winningBlock;
            this._winningBlockchain = winningBlockchain;

            // notify the blockchain worker after updating winning block
            this.blockchainWorker.NotifyWork();

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
