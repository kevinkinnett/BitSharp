using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.CoreProtocol.ExtensionMethods;
using BitSharp.Database;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Storage.ExtensionMethods;
using BitSharp.WireProtocol;
using NGenerics.DataStructures.Trees;
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

namespace BitSharp.CoreProtocol
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
    public class BlockchainManager : IDisposable
    {
        private readonly ScriptLogger logger;
        //TODO
        public readonly StorageManager storageManager;

        private readonly Block genesisBlock;

        private Blockchain currentBlockchain;
        private ReaderWriterLock currentBlockchainLock;
        private ImmutableArray<UInt256>? currentBlockchainLocatorHashes;

        private readonly ConcurrentSet<UInt256> validBlocks;
        private readonly ConcurrentSet<UInt256> invalidBlocks;

        private readonly LoopingWorker metadataWorker;
        private readonly AutoResetEvent metadataWorkerEvent;
        private readonly LoopingWorker chainingWorker;
        private readonly AutoResetEvent chainingWorkerEvent;
        private readonly LoopingWorker validationWorker;
        private readonly AutoResetEvent validationWorkerEvent;
        private readonly LoopingWorker blockchainWorker;
        private readonly AutoResetEvent blockchainWorkerEvent;

        // statistics
        private readonly Stopwatch totalStopwatch;

        public BlockchainManager(Block genesisBlock, ScriptLogger logger, StorageManager storageManager)
        {
            this.logger = logger;
            this.storageManager = storageManager;

            this.validBlocks = new ConcurrentSet<UInt256>();
            this.invalidBlocks = new ConcurrentSet<UInt256>();

            this.genesisBlock = genesisBlock;

            var genesisBlockMetadata =
                new BlockMetadata
                (
                    BlockHash: genesisBlock.Hash,
                    PreviousBlockHash: genesisBlock.Header.PreviousBlock,
                    Height: 0,
                    TotalWork: genesisBlock.CalculateWork(),
                    IsValid: true
                );

            var genesisBlockchain =
                new Blockchain
                (
                    BlockList: ImmutableList.Create(genesisBlockMetadata),
                    Utxo: new BinarySearchTree<TxOutputKey, object>() // genesis block coinbase is not included in utxo, it is unspendable
                );

            this.currentBlockchain = genesisBlockchain;
            this.currentBlockchainLock = new ReaderWriterLock();

            // write genesis block out to storage
            this.storageManager.blockDataCache.UpdateValue(genesisBlock.Hash, genesisBlock);
            this.storageManager.blockMetadataCache.UpdateValue(genesisBlockMetadata.BlockHash, genesisBlockMetadata);

            // start loading the existing state from storage
            //LoadExistingState();

            // wait for storage to finish its initial loading of keys
            //this.storageManager.blockDataCache.WaitForLoad();
            //this.storageManager.blockHeaderCache.WaitForLoad();
            //this.storageManager.blockMetadataCache.WaitForLoad();

            // warm up storage
            //new MethodTimer().Time("blockHeaderCache", () =>
            //    this.storageManager.blockHeaderCache.GetAllValuesFromStorage().ToList());
            //new MethodTimer().Time("blockMetadataCache", () =>
            //    this.storageManager.blockMetadataCache.GetAllValuesFromStorage().ToList());

            // start timing
            this.totalStopwatch = new Stopwatch();
            this.totalStopwatch.Start();

            // create worker event to fire when data is added
            this.metadataWorkerEvent = new AutoResetEvent(false);
            this.chainingWorkerEvent = new AutoResetEvent(false);
            this.validationWorkerEvent = new AutoResetEvent(false);
            this.blockchainWorkerEvent = new AutoResetEvent(false);

            //TODO fire event at some point in the future, try to allow a backlog of work to build up
            this.storageManager.blockDataCache.OnAddition += block => { this.needsNotify = true; WriteMetadata(block); };
            this.storageManager.blockMetadataCache.OnAddition += block => { this.needsNotify = true; };
            this.storageManager.blockDataCache.OnModification += block => { this.needsNotify = true; };
            this.storageManager.blockMetadataCache.OnModification += block => { this.needsNotify = true; };
            this.notifyThread = new Thread(NotifyWorkers);
            this.notifyThread.Start();

            //TODO
            //this.storageManager.blockMetadataCache.OnAddition += block => this.blockchainWorkerEvent.Set();
            //this.storageManager.blockMetadataCache.OnModification += block => this.blockchainWorkerEvent.Set();

            //start workers
            //TODO on polling intervals
            this.metadataWorker = new PollingEventWorker(TimeSpan.FromSeconds(30), this.metadataWorkerEvent, MetadataWorker);
            this.chainingWorker = new PollingEventWorker(TimeSpan.FromSeconds(5), this.chainingWorkerEvent, ChainingWorker);
            this.validationWorker = new PollingEventWorker(TimeSpan.FromSeconds(5), this.validationWorkerEvent, ValidationWorker);
            this.blockchainWorker = new PollingEventWorker(TimeSpan.FromSeconds(5), this.blockchainWorkerEvent, BlockchainWorker);
        }

        private long notifyCount = 0;
        private readonly AutoResetEvent notifyEvent = new AutoResetEvent(false);
        private readonly Thread notifyThread;
        private bool notifyDone;
        private bool needsNotify;
        private void NotifyWorkers()
        {
            while (!notifyDone)
            {
                if (this.needsNotify)
                {
                    this.needsNotify = false;

                    this.metadataWorkerEvent.Set();
                    this.chainingWorkerEvent.Set();
                    this.validationWorkerEvent.Set();
                    this.blockchainWorkerEvent.Set();
                }

                //TODO
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                this.notifyEvent.Set();
                notifyCount++;
            }
        }

        public void Dispose()
        {
            this.notifyDone = true;
            this.notifyThread.Join();

            this.metadataWorker.Dispose();
            this.chainingWorker.Dispose();
            this.validationWorker.Dispose();
            this.blockchainWorker.Dispose();
        }

        public Blockchain CurrentBlockchain
        {
            get { return this.currentBlockchain; }
        }

        public ImmutableArray<UInt256> CurrentBlockchainLocatorHashes
        {
            get
            {
                var currentBlockchainLocatorHashes = this.currentBlockchainLocatorHashes;
                if (currentBlockchainLocatorHashes == null)
                {
                    this.currentBlockchainLock.DoWrite(() =>
                    {
                        currentBlockchainLocatorHashes = BlockchainManager.CalculateBlockLocatorHashes(this.currentBlockchain.BlockList);
                        this.currentBlockchainLocatorHashes = currentBlockchainLocatorHashes;
                    });
                }

                return currentBlockchainLocatorHashes.Value;
            }
        }

        //TODO
        public void WaitForUpdate()
        {
            //Debug.WriteLine("Waiting for metadataWorker");
            this.notifyEvent.WaitOne();
            this.notifyEvent.WaitOne();
            this.metadataWorker.WaitForStall();
            //Debug.WriteLine("Finished Waiting for metadataWorker");

            //Debug.WriteLine("Waiting for chainingWorker");
            this.notifyEvent.WaitOne();
            this.notifyEvent.WaitOne();
            this.chainingWorker.WaitForStall();
            //Debug.WriteLine("Finished Waiting for chainingWorker");

            //Debug.WriteLine("Waiting for blockchainWorker");
            this.notifyEvent.WaitOne();
            this.notifyEvent.WaitOne();
            this.blockchainWorker.WaitForStall();
            //Debug.WriteLine("Finished Waiting for blockchainWorker");
        }

        private bool MetadataWorker() //out long txCount, out long inputCount)
        {
            var wasMissingAdded = false;
            //Debug.WriteLine("MetadataWorker.Enter");

            new MethodTimer(false).Time(() =>
            {
                // find any block's that don't have a metadata entry
                //var missingBlockMetadataHashes =
                //    this.storageManager.blockDataCache.GetAllKeys()//.AsParallel()
                //    .GroupJoin(
                //        this.storageManager.blockMetadataCache.GetAllKeys(),//.AsParallel(),
                //        blockHash => blockHash,
                //        blockHash => blockHash,
                //        (blockData, blockMetadata) => new { blockData, blockMetadata }
                //    )
                //    .SelectMany(x => x.blockMetadata.DefaultIfEmpty().Where(y => y.IsDefault).Select(z => x.blockData));

                var missingBlockMetadataHashes = new MethodTimer(false).Time("missingBlockMetadataHashes", () =>
                    this.storageManager.blockDataCache.GetAllKeys()
                    .Except(this.storageManager.blockMetadataCache.GetAllKeys()).ToList());

                // add the metadata entries
                foreach (var blockHash in missingBlockMetadataHashes)
                {
                    if (WriteMetadata(blockHash))
                    {
                        wasMissingAdded = true;
                    }
                }
            });

            //Debug.WriteLine("MetadataWorker.Exit: {0}".Format2(wasMissingAdded));
            return wasMissingAdded;
        }

        private bool WriteMetadata(UInt256 blockHash)
        {
            if (!this.storageManager.blockMetadataCache.ContainsKey(blockHash))
            {
                BlockHeader blockHeader;
                if (this.storageManager.blockHeaderCache.TryGetValue(blockHash, out blockHeader))
                {
                    long? height = null;
                    BigInteger? totalWork = null;

                    BlockMetadata prevBlockMetdata;
                    if (this.storageManager.blockMetadataCache.TryGetValue(blockHeader.PreviousBlock, out prevBlockMetdata))
                    {
                        if (prevBlockMetdata.Height != null && prevBlockMetdata.TotalWork != null)
                        {
                            height = prevBlockMetdata.Height.Value + 1;
                            totalWork = prevBlockMetdata.TotalWork + blockHeader.CalculateWork();
                        }
                    }

                    var blockMetadata = new BlockMetadata
                    (
                        blockHash,
                        PreviousBlockHash: blockHeader.PreviousBlock,
                        Height: height,
                        TotalWork: totalWork,
                        IsValid: null
                    );

                    this.storageManager.blockMetadataCache.CreateValue(blockHash, blockMetadata);

                    return true;
                }
            }

            return false;
        }

        private bool ChainingWorker()
        {
            var wasAnyChainAdvanced = false;

            var chainedBlocks = new BinarySearchTree<UInt256, object>();
            var wasLoopAdvanced = true;
            var includeStorage = true;
            while (wasLoopAdvanced)
            {
                wasLoopAdvanced = false;

                // grab a snapshot of pending blocks
                var pendingBlockMetadata =
                    this.storageManager.blockMetadataCache.GetPendingValues()
                    .ToDictionary(x => x.Key, x => x.Value);

                // get chainable metadata from storage and pending
                var blockMetadataStorage = new BlockMetadataStorage();
                var chainableBlocks =
                    blockMetadataStorage.FindChainableBlocks(pendingBlockMetadata, includeStorage)
                    .ToDictionary(x => x.BlockHash, x => x);

                //TODO
                // only check storage on first loop iteration
                //includeStorage = false;

                // start processing
                while (chainableBlocks.Count > 0)
                {
                    // grab item to be processed
                    var key = chainableBlocks.Keys.First();
                    var chainable = chainableBlocks[key];

                    // remove item to be processed from the list
                    chainableBlocks.Remove(key);

                    // check if this block was already processed
                    if (chainedBlocks.ContainsKey(key))
                        break;
                    chainedBlocks.Add(key, null);

                    //TODO add Work to BlockMetadata on initial creation, save the block header lookup here
                    BlockMetadata previousBlock;
                    if (
                        this.storageManager.blockMetadataCache.TryGetValue(chainable.PreviousBlockHash, out previousBlock)
                        && previousBlock.Height != null && previousBlock.TotalWork != null
                    )
                    {
                        BlockHeader chainableHeader;
                        if (this.storageManager.blockHeaderCache.TryGetValue(chainable.BlockHash, out chainableHeader))
                        {
                            Debug.Assert(previousBlock.BlockHash == chainable.PreviousBlockHash);

                            var newMetadata = new BlockMetadata
                            (
                                BlockHash: chainable.BlockHash,
                                PreviousBlockHash: chainable.PreviousBlockHash,
                                Height: previousBlock.Height.Value + 1,
                                TotalWork: previousBlock.TotalWork.Value + chainableHeader.CalculateWork(),
                                IsValid: chainable.IsValid
                            );

                            this.storageManager.blockMetadataCache.UpdateValue(newMetadata.BlockHash, newMetadata);

                            Debug.WriteLineIf(newMetadata.Height.Value % 1000 == 0, "Chained block {0} at height {1}, total work: {2}".Format2(newMetadata.BlockHash.ToHexNumberString(), newMetadata.Height.Value, newMetadata.TotalWork.Value.ToString("X")));
                            wasAnyChainAdvanced = true;
                            wasLoopAdvanced = true;

                            //TODO
                            //// see if any unchained blocks can chain off this newly added link
                            //if (unchainedBlocksByPrevious.ContainsKey(newMetadata.BlockHash))
                            //{
                            //    // lookup the metadata for any blocks that can now chain
                            //    var proceeding2 = new ConcurrentSet<BlockMetadata>();
                            //    foreach (var proceedingHash in unchainedBlocksByPrevious[newMetadata.BlockHash])
                            //    {
                            //        BlockMetadata proceedingMetadata;
                            //        if (this.storageManager.blockMetadataCache.TryGetValue(proceedingHash, out proceedingMetadata))
                            //        {
                            //            proceeding2.TryAdd(proceedingMetadata);
                            //        }
                            //    }

                            //    // add the newly chainable blocks to the list to be processed
                            //    chainedWithProceeding.AddOrUpdate(
                            //        newMetadata,
                            //        newKey => new ConcurrentSet<BlockMetadata>(),
                            //        (existingKey, existingValue) => existingValue
                            //    )
                            //    .UnionWith(proceeding2);
                            //}
                        }
                    }
                }
            }

            //// find any blocks that have been
            //var unvalidatedBlocks = this.blockMetadataCache.GetAllValues().Where(x => x.IsValid == null);

            //Debug.WriteLine("ChainingWorker.Exit: {0}".Format2(wasAnyChainAdvanced));
            return wasAnyChainAdvanced;
        }

        private bool ValidationWorker()
        {
            return false;
        }

        private Stopwatch validateStopwatch = new Stopwatch();
        private bool BlockchainWorker() //out long txCount, out long inputCount)
        {
            // grab a snapshot of pending blocks
            var pendingBlockMetadata =
                this.storageManager.blockMetadataCache.GetPendingValues()
                .ToDictionary(x => x.Key, x => x.Value);

            // get winning chain metadata from storage, giving flush pending blocks priority
            var blockMetadataStorage = new BlockMetadataStorage();
            var winningChainedBlocks =
                blockMetadataStorage.FindWinningChainedBlocks(pendingBlockMetadata)
                .ToDictionary(x => x.BlockHash, x => x);

            // check if the current blockchain is no longer among the winning chains, only advance/switch chains then
            if (!winningChainedBlocks.ContainsKey(this.currentBlockchain.RootBlockHash))
            {
                //TODO ordering will need to follow actual bitcoin rules to ensure the same winning chaing is always selected
                foreach (var candidateBlock in winningChainedBlocks.Values)
                {
                    // try to advance the blockchain with the new winning block
                    if (UseWinningBlock(candidateBlock))
                    {
                        // a new winning chain was successfully used
                        return true;
                    }
                }
            }

            return false;
        }

        private bool UseWinningBlock(BlockMetadata blockMetadata)
        {
            Debug.WriteLine("Winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));

            // take snapshots
            var currentBlockchain = this.currentBlockchain;
            currentBlockchain = new Blockchain(currentBlockchain.BlockList, currentBlockchain.Utxo.Clone());
            var newChainBlockMetadata = blockMetadata;
            var newChainBlockList = new[] { newChainBlockMetadata.BlockHash }.ToList();

            // check height difference between chains, they will be roll backed before checking for the last common ancestor
            var heightDelta = (int)blockMetadata.Height.Value - this.currentBlockchain.Height;

            // if current chain is shorter, roll new chain back to current chain's height
            if (heightDelta > 0)
            {
                for (var i = 0; i < heightDelta; i++)
                {
                    if (!this.storageManager.blockMetadataCache.TryGetValue(newChainBlockMetadata.PreviousBlockHash, out newChainBlockMetadata))
                    {
                        Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                        Debugger.Break();
                        return false;
                    }

                    // ensure that height is as expected while looking up previous blocks
                    if (newChainBlockMetadata.Height != blockMetadata.Height - i - 1)
                    {
                        Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                        Debugger.Break();
                        return false;
                    }

                    // keep track of rolled back data on the new blockchain
                    newChainBlockList.Add(newChainBlockMetadata.BlockHash);
                }
            }
            // if current chain is longer, roll it back to new chain's height
            else if (heightDelta < 0)
            {
                for (var i = 0; i < -heightDelta; i++)
                {
                    if (!TryRollbackBlockchain(ref currentBlockchain))
                    {
                        Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                        Debugger.Break();
                        return false;
                    }
                }
            }

            Debug.Assert(newChainBlockMetadata.Height == currentBlockchain.Height);

            //TODO continue looking backwards while processing moves forward to check the double check
            //TODO the blockchain history back to genesis? only look at height, work, valid bits in
            //TODO the metadata, sync and check this task at the end before updating current blockchain,
            //TODO if any error is ever found, mark everything after it as invalid or unprocessed, the
            //TODO processor could get stuck otherwise trying what it thinks is the winning chain over and over

            // with both chains at the same height, roll back to last common ancestor
            while (newChainBlockMetadata.BlockHash != currentBlockchain.RootBlockHash)
            {
                // roll back current block chain
                if (!TryRollbackBlockchain(ref currentBlockchain))
                {
                    Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                    Debugger.Break();
                    return false;
                }

                // roll back new block chain
                if (!this.storageManager.blockMetadataCache.TryGetValue(newChainBlockMetadata.PreviousBlockHash, out newChainBlockMetadata))
                {
                    Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                    Debugger.Break();
                    return false;
                }

                // ensure that height is as expected while looking up previous blocks
                if (newChainBlockMetadata.Height != currentBlockchain.Height)
                {
                    Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                    Debugger.Break();
                    return false;
                }

                // keep track of rolled back data on the new blockchain
                newChainBlockList.Add(newChainBlockMetadata.BlockHash);
            }

            var currentBlockCount = 0L;
            var currentTxCount = 0L;
            var currentInputCount = 0L;
            var totalTxCount = 0L;
            var totalInputCount = 0L;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // with last common ancestor found and utxo rolled back to that point, calculate the new blockchain
            // use ImmutableList for BlockList during modification
            var newBlockchain = new Blockchain
            (
                BlockList: currentBlockchain.BlockList.ToImmutableList(),
                Utxo: currentBlockchain.Utxo
            );

            // work list will have last items added first, reverse
            newChainBlockList.Reverse();
            // skip the first item which will be the last common ancestor
            newChainBlockList.RemoveAt(0);

            // setup look-ahead task for utxo calculation
            var blocksProcessed = 0;
            var blocksProcessedEvent = new AutoResetEvent(false);
            Block lastBlock = this.genesisBlock;
            var cacheTask = Task.Run(() =>
            {
                //for (var i = 0; i < blockchain.Count; i++)
                Parallel.For(0, newChainBlockList.Count, (i, loopState) =>
                {
                    if (blocksProcessed != -1)
                    {
                        if (lastBlock.IsDefault)
                        {
                            Debugger.Break();
                        }

                        var lastBlockSize = lastBlock.SizeEstimate;
                        if (lastBlockSize == 0)
                        {
                            Debugger.Break();
                        }
                        var cacheLookAhead = this.storageManager.blockDataCache.maxCacheMemorySize / 2 / lastBlockSize;

                        if (i - blocksProcessed > cacheLookAhead)
                        {
                            while (i - blocksProcessed > cacheLookAhead && blocksProcessed != -1)
                                blocksProcessedEvent.WaitOne(TimeSpan.FromMilliseconds(50));
                        }

                        // check that a block that has already been processed doesn't get loaded
                        if (i > blocksProcessed)
                        {
                            // don't fail if a block or metadata can't be found here, the main loop below will take care of that
                            Block block;
                            if (this.storageManager.blockDataCache.TryGetValue(newChainBlockList[i], out block))
                            {
                                //TODO rewrite transactions
                                //Parallel.ForEach(block.Transactions, tx =>
                                //    this.storageManager.txStorage.CreateValue(tx.Hash, tx));
                            }

                            BlockMetadata nextBlockMetadata;
                            this.storageManager.blockMetadataCache.TryGetValue(newChainBlockList[i], out nextBlockMetadata);
                        }
                    }
                    else
                    {
                        loopState.Break();
                    }
                });
            });
            try
            {
                // starting calculating new utxo
                for (var i = 0; i < newChainBlockList.Count; i++)
                {
                    Block nextBlock;
                    if (!this.storageManager.blockDataCache.TryGetValue(newChainBlockList[i], out nextBlock))
                    {
                        Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                        Debugger.Break();
                        return false;
                    }
                    BlockMetadata nextBlockMetadata;
                    if (!this.storageManager.blockMetadataCache.TryGetValue(newChainBlockList[i], out nextBlockMetadata))
                    {
                        Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                        Debugger.Break();
                        return false;
                    }

                    lastBlock = nextBlock;
                    blocksProcessed++;
                    blocksProcessedEvent.Set();

                    // calculate the new block utxo, double spends will be checked for
                    long txCount, inputCount;
                    if (CalculateUtxo(newBlockchain.Height, nextBlock, newBlockchain.Utxo, out txCount, out inputCount))
                    {
                        // calculate the next required target
                        var requiredTarget = GetRequiredNextTarget(newBlockchain);

                        //TODO rewrite transactions
                        //Parallel.ForEach(nextBlock.Transactions, tx =>
                        //    this.storageManager.txStorage.CreateValue(tx.Hash, tx));

                        // validate the block
                        // validation utxo includes all transactions added in the same block, any double spends will have failed the block above
                        validateStopwatch.Start();
                        if (true || BlockValidator.ValidateBlock(requiredTarget, newBlockchain.Height, nextBlock, this.storageManager, this.logger))
                        {
                            validateStopwatch.Stop();

                            // create the next link in the new blockchain
                            newBlockchain =
                                new Blockchain
                                (
                                    BlockList: newBlockchain.BlockList.Add(nextBlockMetadata),
                                    Utxo: newBlockchain.Utxo
                                );

                            // blockchain processing statistics
                            currentBlockCount++;
                            currentTxCount += txCount;
                            currentInputCount += inputCount;
                            totalTxCount += txCount;
                            totalInputCount += inputCount;

                            var txInterval = 100.THOUSAND();
                            if (
                                newBlockchain.Height % 10.THOUSAND() == 0
                                || (totalTxCount % txInterval < (totalTxCount - txCount) % txInterval || txCount >= txInterval)
                                || i == newChainBlockList.Count - 1)
                            {
                                var currentBlockRate = (float)currentBlockCount / ((float)stopwatch.ElapsedMilliseconds / 1000);
                                var currentTxRate = (float)currentTxCount / ((float)stopwatch.ElapsedMilliseconds / 1000);
                                var currentInputRate = (float)currentInputCount / ((float)stopwatch.ElapsedMilliseconds / 1000);

                                Debug.WriteLine(
                                    string.Join("\n",
                                        new string('-', 80),
                                        "Height: {0,10} | Date: {1} | Duration: {7} hh:mm:ss | Validation: {8} hh:mm:ss | Blocks/s: {2,7} | Tx/s: {3,7} | Inputs/s: {4,7} | Total Tx: {5,7} | Total Inputs: {6,7} | Utxo Size: {9,7}",
                                        new string('-', 80)
                                    )
                                    .Format2
                                    (
                                        newBlockchain.Height.ToString("#,##0"),
                                        nextBlock.Header.Time.UnixTimeToDateTime().ToString("yyyy-MM-dd"),
                                        currentBlockRate.ToString("#,##0"),
                                        currentTxRate.ToString("#,##0"),
                                        currentInputRate.ToString("#,##0"),
                                        totalTxCount.ToString("#,##0"),
                                        totalInputCount.ToString("#,##0"),
                                        totalStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                                        validateStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                                        newBlockchain.Utxo.Count.ToString("#,##0")
                                    ));

                                currentBlockCount = 0;
                                currentTxCount = 0;
                                currentInputCount = 0;
                                stopwatch.Reset();
                                stopwatch.Start();
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                            Debugger.Break();
                            return false;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Aborting winning chained block {0} at height {1}, total work: {2}".Format2(blockMetadata.BlockHash.ToHexNumberString(), blockMetadata.Height.Value, blockMetadata.TotalWork.Value.ToString("X")));
                        Debugger.Break();
                        return false;
                    }
                }

                // use ImmutableArray for BlockList after modification
                newBlockchain = new Blockchain
                (
                    BlockList: newBlockchain.BlockList.ToImmutableArray(),
                    Utxo: newBlockchain.Utxo
                );

                // update current blockchain with new
                this.currentBlockchainLock.DoWrite(() =>
                {
                    this.currentBlockchain = newBlockchain;
                    this.currentBlockchainLocatorHashes = null;
                });

                // collect after processing, lots of memory traffic
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

                return true;
            }
            finally
            {
                // ensure that look-ahead task is properly cleaned up
                blocksProcessed = -1;
                blocksProcessedEvent.Set();
                cacheTask.Wait();
            }
        }

        private UInt256 GetRequiredNextTarget(Blockchain blockchain)
        {
            // lookup the latest block on the current blockchain
            BlockHeader currentBlockHeader;
            if (!this.storageManager.blockHeaderCache.TryGetValue(blockchain.RootBlockHash, out currentBlockHeader))
            {
                Debug.WriteLine("inconsistent state");
                Debugger.Break();
                Environment.Exit(-2);
            }

            var nextHeight = blockchain.Height + 1;

            // use genesis block difficulty if first adjusment interval has not yet been reached
            if (nextHeight < CoreRules.DifficultyInternal)
            {
                return CoreRules.BitsToTarget(this.genesisBlock.Header.Bits);
            }
            // not on an adjustment interval, reuse current block's target
            else if (nextHeight % CoreRules.DifficultyInternal != 0)
            {
                return CoreRules.BitsToTarget(currentBlockHeader.Bits);
            }
            // on an adjustment interval, calculate the required next target
            else
            {
                // get the block (difficultyInterval - 1) blocks ago
                var startBlockMetadata = blockchain.BlockList.Reverse().Skip(CoreRules.DifficultyInternal - 1).First();
                BlockHeader startBlockHeader;
                if (!this.storageManager.blockHeaderCache.TryGetValue(startBlockMetadata.BlockHash, out startBlockHeader))
                {
                    Debug.WriteLine("inconsistent state");
                    Debugger.Break();
                    Environment.Exit(-2);
                }

                var actualTimespan = (long)currentBlockHeader.Time - (long)startBlockHeader.Time;
                var targetTimespan = CoreRules.DifficultyTargetTimespan;

                // limit adjustment to 4x or 1/4x
                if (actualTimespan < targetTimespan / 4)
                    actualTimespan = targetTimespan / 4;
                else if (actualTimespan > targetTimespan * 4)
                    actualTimespan = targetTimespan * 4;

                // calculate the new target
                var target = CoreRules.BitsToTarget(startBlockHeader.Bits);
                target *= actualTimespan;
                target /= targetTimespan;

                // make sure target isn't too high (too low difficulty)
                if (target > CoreRules.HighestTarget)
                    target = CoreRules.HighestTarget;

                return target;
            }
        }

        //TODO
        private void LoadExistingState()
        {
        }

        //TODO move out of here?, it's a p2p thing
        private static ImmutableArray<UInt256> CalculateBlockLocatorHashes(IImmutableList<BlockMetadata> blockHashes)
        {
            var blockLocatorHashes = new List<UInt256>();

            if (blockHashes.Count > 0)
            {
                var step = 1;
                var start = 0;
                for (var i = blockHashes.Count - 1; i > 0; i -= step, start++)
                {
                    if (start >= 10)
                        step *= 2;

                    blockLocatorHashes.Add(blockHashes[i].BlockHash);
                }
                blockLocatorHashes.Add(blockHashes[0].BlockHash);
            }

            return blockLocatorHashes.ToImmutableArray();
        }

        //TODO add baseline validation function to call before CalculateUtxo

        private bool CalculateUtxo(long blockHeight, Block block, BinarySearchTree<TxOutputKey, object> currentUtxo, out long txCount, out long inputCount)
        {
            //var stopwatch = new Stopwatch();
            //stopwatch.Start();

            txCount = 0;
            inputCount = 0;

            //TODO apply real coinbase rule
            // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
            var coinbaseTx = block.Transactions[0];

            var removeTxOutputs = new Dictionary<TxOutputKey, object>();
            var addTxOutputs = new Dictionary<TxOutputKey, object>();

            // add the coinbase outputs to the list to be added to the utxo
            for (var outputIndex = 0; outputIndex < coinbaseTx.Outputs.Length; outputIndex++)
            {
                var txOutputKey = new TxOutputKey(coinbaseTx.Hash, outputIndex);

                // add transaction output to the list to be added to the utxo, if it isn't a duplicate
                //TODO don't include genesis block coinbase in utxo as it's not spendable... but should it actually be excluded from UTXO?
                //TODO should a duplicate transaction get ignored or overwrite the original?
                if (blockHeight > 0 && !currentUtxo.ContainsKey(txOutputKey))
                {
                    addTxOutputs.Add(txOutputKey, null);
                }
                else
                {
                    //TODO this needs to be tracked so that blocks can be rolled back accurately
                    //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                }
            }

            // check for double spends
            for (var txIndex = 1; txIndex < block.Transactions.Length; txIndex++)
            {
                var tx = block.Transactions[txIndex];
                txCount++;

                for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
                {
                    var input = tx.Inputs[inputIndex];
                    inputCount++;

                    // get the previous transaction output key
                    var prevTxOutputKey = new TxOutputKey(input.PreviousTransactionHash, input.PreviousTransactionIndex.ToIntChecked());

                    // check for a double spend within the same block
                    if (removeTxOutputs.ContainsKey(prevTxOutputKey))
                    {
                        Debug.WriteLine("Failing block {0} at height {1} tx {2}: Double spend within same block", block.Hash.ToHexNumberString(), blockHeight, txIndex);
                        Debugger.Break();
                        return false;
                    }

                    // add the previous output to the list to be removed from the utxo
                    removeTxOutputs.Add(prevTxOutputKey, null);

                    // check that previous transaction output is in the utxo or in the same block
                    if (!currentUtxo.ContainsKey(prevTxOutputKey) && !addTxOutputs.ContainsKey(prevTxOutputKey))
                    {
                        Debugger.Break();
                        return false;
                    }
                }

                for (var outputIndex = 0; outputIndex < tx.Outputs.Length; outputIndex++)
                {
                    var output = tx.Outputs[outputIndex];

                    // add the output to the list to be added to the utxo
                    var txOutputKey = new TxOutputKey(tx.Hash, outputIndex);

                    // add transaction output to the list to be added to the utxo, if it isn't a duplicate
                    if (!currentUtxo.ContainsKey(txOutputKey)) //TODO should a duplicate transaction get ignored or overwrite the original?
                    {
                        addTxOutputs.Add(txOutputKey, null);
                    }
                }
            }

            // calculate new utxo to verify transactions with, include anything added in this block (double spends have already been checked for)
            foreach (var key in addTxOutputs.Keys)
                currentUtxo.Add(key, null);

            // calculate final utxo to use after transacctions are spent
            foreach (var key in removeTxOutputs.Keys)
                currentUtxo.Remove(key);

            //stopwatch.Stop();
            //Debug.WriteLineIf(blockHeight % 1.THOUSAND() == 0, "{0:#,##0.0000000}s, {1,7:#,##0}Hz, {2,7:#,##0}".Format2(stopwatch.EllapsedSecondsFloat(), 1 / stopwatch.EllapsedSecondsFloat(), utxoChurn));

            return true;
        }

        private bool TryRollbackBlockchain(ref Blockchain blockchain)
        {
            if (blockchain.BlockCount == 0)
            {
                blockchain = default(Blockchain);
                Debugger.Break();
                return false;
            }

            Block block;
            if (this.storageManager.blockDataCache.TryGetValue(blockchain.RootBlockHash, out block))
            {
                //TODO shouldn't be exception here
                try
                {
                    RollbackUtxo(blockchain.Height, block, blockchain.Utxo);
                }
                catch (Exception e)
                {
                    blockchain = default(Blockchain);
                    Debugger.Break();
                    return false;
                }

                blockchain = new Blockchain
                (
                    BlockList: blockchain.BlockList.RemoveAt(blockchain.BlockCount - 1),
                    Utxo: blockchain.Utxo
                );

                return true;
            }
            else
            {
                blockchain = default(Blockchain);
                Debugger.Break();
                return false;
            }
        }

        private void RollbackUtxo(long blockHeight, Block block, BinarySearchTree<TxOutputKey, object> currentUtxo)
        {
            //TODO apply real coinbase rule
            // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
            var coinbaseTx = block.Transactions[0];

            var removeTxOutputs = new Dictionary<TxOutputKey, object>();
            var addTxOutputs = new Dictionary<TxOutputKey, object>();

            for (var outputIndex = 0; outputIndex < coinbaseTx.Outputs.Length; outputIndex++)
            {
                var txOutputKey = new TxOutputKey(coinbaseTx.Hash, outputIndex);
                if (blockHeight > 0)
                {
                    addTxOutputs.Add(txOutputKey, null);
                }
            }

            for (var txIndex = 1; txIndex < block.Transactions.Length; txIndex++)
            {
                var tx = block.Transactions[txIndex];

                for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
                {
                    var input = tx.Inputs[inputIndex];
                    var prevTxOutputKey = new TxOutputKey(input.PreviousTransactionHash, input.PreviousTransactionIndex.ToIntChecked());
                    removeTxOutputs.Add(prevTxOutputKey, null);
                }

                for (var outputIndex = 0; outputIndex < tx.Outputs.Length; outputIndex++)
                {
                    var output = tx.Outputs[outputIndex];
                    var txOutputKey = new TxOutputKey(tx.Hash, outputIndex);
                    //TODO what if a transaction wasn't added to the utxo because it already existed?
                    //TODO the block would still pass without adding the tx to its utxo, but here it would get rolled back
                    //TODO maybe a flag bit to track this?
                    addTxOutputs.Add(txOutputKey, null);
                }
            }

            //TODO
            //Debug.Assert(currentUtxo.Intersect(addTxOutputs).Count == addTxOutputs.Count);
            //Debug.Assert(currentUtxo.Intersect(removeTxOutputs).Count == 0);

            foreach (var key in addTxOutputs.Keys)
                currentUtxo.Remove(key);

            foreach (var key in removeTxOutputs.Keys)
                currentUtxo.Add(key, null);
        }
    }

    namespace ExtensionMethods
    {
        internal static class BlockchainExtensionMethods
        {
            public static BigInteger CalculateWork(this Block block)
            {
                return CalculateWork(block.Header);
            }

            //TDOO name...
            private static readonly BigInteger WorkTarget = new BigInteger(1) << 256;
            public static BigInteger CalculateWork(this BlockHeader blockHeader)
            {
                //TODO should 
                return WorkTarget / (BigInteger)CoreRules.BitsToTarget(blockHeader.Bits);
            }

            public static BinarySearchTree<TKey, TValue> Clone<TKey, TValue>(this BinarySearchTree<TKey, TValue> tree)
            {
                return new MethodTimer().Time(() =>
                {
                    var clone = new BinarySearchTree<TKey, TValue>();
                    foreach (var item in tree)
                    {
                        clone.Add(item.Key, item.Value);
                    }

                    return clone;
                });
            }
        }
    }
}
