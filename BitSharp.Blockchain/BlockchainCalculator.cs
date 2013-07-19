using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain.ExtensionMethods;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Data;
using System.IO;
using BitSharp.Storage;

namespace BitSharp.Blockchain
{
    public class BlockchainCalculator
    {
        private readonly IBlockchainRules _rules;
        private readonly CacheContext _cacheContext;
        private readonly CancellationToken shutdownToken;
        //TODO
        private readonly Stopwatch validateStopwatch = new Stopwatch();

        public BlockchainCalculator(IBlockchainRules rules, CacheContext cacheContext, CancellationToken shutdownToken)
        {
            this._rules = rules;
            this._cacheContext = cacheContext;
            this.shutdownToken = shutdownToken;
        }

        public IBlockchainRules Rules { get { return this._rules; } }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public Data.Blockchain CalculateBlockchainFromExisting(Data.Blockchain currentBlockchain, BlockMetadata targetBlockMetadata, out List<MissingDataException> missingData, CancellationToken cancelToken, Action<Data.Blockchain> onProgress = null)
        {
            Debug.WriteLine("Winning chained block {0} at height {1}, total work: {2}".Format2(targetBlockMetadata.BlockHash.ToHexNumberString(), targetBlockMetadata.Height.Value, targetBlockMetadata.TotalWork.Value.ToString("X")));

            missingData = new List<MissingDataException>();

            // take snapshots
            var newChainBlockMetadata = targetBlockMetadata;
            var newChainBlockList = new[] { newChainBlockMetadata.BlockHash }.ToList();

            // check height difference between chains, they will be roll backed before checking for the last common ancestor
            var heightDelta = (int)targetBlockMetadata.Height.Value - currentBlockchain.Height;

            // if current chain is shorter, roll new chain back to current chain's height
            if (heightDelta > 0)
            {
                Debug.WriteLine("Rolling winning chainblock back to height of current chainblock: {0:#,##0} steps".Format2(heightDelta));

                for (var i = 0; i < heightDelta; i++)
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    newChainBlockMetadata = this.CacheContext.GetBlockMetadata(newChainBlockMetadata.PreviousBlockHash);

                    // check that block metadata has been chained
                    if (newChainBlockMetadata.Height == null)
                    {
                        //TODO why is this happening outside of initial startup?
                        //Debugger.Break();
                        throw new MissingDataException(DataType.BlockMetadata, newChainBlockMetadata.BlockHash);
                    }

                    // ensure that height is as expected while looking up previous blocks
                    if (newChainBlockMetadata.Height != targetBlockMetadata.Height - i - 1)
                    {
                        throw new ValidationException();
                    }

                    // keep track of rolled back data on the new blockchain
                    newChainBlockList.Add(newChainBlockMetadata.BlockHash);
                }
            }
            // if current chain is longer, roll it back to new chain's height
            else if (heightDelta < 0)
            {
                Debug.WriteLine("Rolling current chainblock back to height of winning chainblock: {0:#,##0} steps".Format2(-heightDelta));

                var rollbackCount = -heightDelta;
                var rollbackList = new List<UInt256>(rollbackCount);
                foreach (var prevBlock in PreviousBlockMetadata(currentBlockchain.RootBlock))
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    rollbackList.Add(prevBlock.BlockHash);
                    heightDelta++;
                    if (heightDelta >= 0)
                        break;
                }

                Debug.Assert(rollbackList.Count == rollbackCount);

                var rollbackIndex = 0;
                foreach (var tuple in BlockAndMetadataLookAhead(rollbackList, null))
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    var block = tuple.Item1;
                    Debug.Assert(currentBlockchain.RootBlockHash == block.Hash);
                    currentBlockchain = RollbackBlockchain(currentBlockchain, block);

                    Debug.WriteLineIf(rollbackIndex % 100 == 0, "Rolling back {0} of {1}".Format2(rollbackIndex + 1, rollbackCount));
                    rollbackIndex++;
                }
            }

            Debug.Assert(newChainBlockMetadata.Height == currentBlockchain.Height);

            //TODO continue looking backwards while processing moves forward to double check
            //TODO the blockchain history back to genesis? only look at height, work, valid bits in
            //TODO the metadata, sync and check this task at the end before updating current blockchain,
            //TODO if any error is ever found, mark everything after it as invalid or unprocessed, the
            //TODO processor could get stuck otherwise trying what it thinks is the winning chain over and over

            Debug.WriteLine("Searching for last common ancestor between current chainblock and winning chainblock");

            // with both chains at the same height, roll back to last common ancestor
            if (newChainBlockMetadata.BlockHash != currentBlockchain.RootBlockHash)
            {
                var rollbackList = new List<UInt256>();
                var currentBlockchainIndex = currentBlockchain.BlockList.Count - 1;
                foreach (var prevBlock in PreviousBlockMetadata(newChainBlockMetadata))
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    if (newChainBlockMetadata.BlockHash == currentBlockchain.BlockList[currentBlockchainIndex].BlockHash)
                    {
                        break;
                    }

                    // roll back new block chain
                    newChainBlockMetadata = prevBlock;

                    // ensure that height is as expected while looking up previous blocks
                    if (newChainBlockMetadata.Height != currentBlockchain.BlockList[currentBlockchainIndex].Height
                        || newChainBlockMetadata.BlockHash != currentBlockchain.BlockList[currentBlockchainIndex].BlockHash)
                    {
                        throw new ValidationException();
                    }

                    // keep track of rolled back data on the new blockchain
                    newChainBlockList.Add(newChainBlockMetadata.BlockHash);

                    // queue up current blockchain rollback
                    rollbackList.Add(currentBlockchain.BlockList[currentBlockchainIndex].BlockHash);

                    currentBlockchainIndex--;
                }

                // roll back current block chain
                foreach (var tuple in BlockAndMetadataLookAhead(rollbackList, null))
                {
                    var block = tuple.Item1;
                    currentBlockchain = RollbackBlockchain(currentBlockchain, block);
                }
            }

            // look up the remainder of the blockchain for transaction lookup
            var newBlockchainHashesBuilder = ImmutableHashSet.CreateBuilder<UInt256>();
            newBlockchainHashesBuilder.UnionWith(newChainBlockList);
            foreach (var prevBlock in PreviousBlockMetadata(newChainBlockMetadata))
            {
                // cooperative loop
                this.shutdownToken.ThrowIfCancellationRequested();
                cancelToken.ThrowIfCancellationRequested();

                newBlockchainHashesBuilder.Add(prevBlock.BlockHash);
            }
            var newBlockchainHashes = newBlockchainHashesBuilder.ToImmutable();

            Debug.WriteLine("Last common ancestor found at block {0}, height {1:#,##0}, begin processing winning blockchain".Format2(currentBlockchain.RootBlockHash.ToHexNumberString(), currentBlockchain.Height));

            // setup statistics
            var totalTxCount = 0L;
            var totalInputCount = 0L;
            var totalStopwatch = new Stopwatch();

            var currentBlockCount = 0L;
            var currentTxCount = 0L;
            var currentInputCount = 0L;
            var currentRateStopwatch = new Stopwatch();

            totalStopwatch.Start();
            currentRateStopwatch.Start();

            // work list will have last items added first, reverse
            newChainBlockList.Reverse();
            // skip the first item which will be the last common ancestor
            newChainBlockList.RemoveAt(0);

            // with last common ancestor found and utxo rolled back to that point, calculate the new blockchain
            // use ImmutableList for BlockList during modification
            var newBlockchain = new Data.Blockchain
            (
                blockList: currentBlockchain.BlockList,
                blockListHashes: currentBlockchain.BlockListHashes,
                utxo: currentBlockchain.Utxo
            );

            // start calculating new utxo
            var index = 0;
            foreach (var tuple in BlockAndMetadataLookAhead(newChainBlockList, newBlockchainHashes))
            {
                // cooperative loop
                this.shutdownToken.ThrowIfCancellationRequested();
                cancelToken.ThrowIfCancellationRequested();

                try
                {
                    // get block and metadata for next link in blockchain
                    var nextBlock = tuple.Item1;
                    var nextBlockMetadata = tuple.Item2;
                    var transactions = tuple.Item3;

                    if (nextBlockMetadata.Height == null || nextBlockMetadata.TotalWork == null)
                        throw new ValidationException();

                    // calculate the new block utxo, double spends will be checked for
                    long txCount, inputCount;
                    var newUtxo = CalculateUtxo(nextBlockMetadata.Height.Value, nextBlock, newBlockchain.Utxo, out txCount, out inputCount);

                    var nextBlockchain =
                        new Data.Blockchain
                        (
                            blockList: newBlockchain.BlockList.Add(nextBlockMetadata),
                            blockListHashes: newBlockchain.BlockListHashes.Add(nextBlockMetadata.BlockHash),
                            utxo: newUtxo
                        );

                    //TODO rewrite transactions
                    //Parallel.ForEach(nextBlock.Transactions, tx =>
                    //    this.storageManager.txStorage.CreateValue(tx.Hash, tx));

                    // validate the block
                    // validation utxo includes all transactions added in the same block, any double spends will have failed the block above
                    validateStopwatch.Start();
                    this.Rules.ValidateBlock(nextBlock, nextBlockchain, transactions);
                    validateStopwatch.Stop();

                    // create the next link in the new blockchain
                    newBlockchain = nextBlockchain;
                    if (onProgress != null)
                        onProgress(newBlockchain);

                    // blockchain processing statistics
                    currentBlockCount++;
                    currentTxCount += txCount;
                    currentInputCount += inputCount;
                    totalTxCount += txCount;
                    totalInputCount += inputCount;

                    var txInterval = 100.THOUSAND();
                    if (
                        newBlockchain.Height % 10.THOUSAND() == 0
                        || (totalTxCount % txInterval < (totalTxCount - txCount) % txInterval || txCount >= txInterval))
                    {
                        LogBlockchainProgress(newBlockchain, totalStopwatch, totalTxCount, totalInputCount, currentRateStopwatch, currentBlockCount, currentTxCount, currentInputCount);

                        currentBlockCount = 0;
                        currentTxCount = 0;
                        currentInputCount = 0;
                        currentRateStopwatch.Reset();
                        currentRateStopwatch.Start();
                    }
                }
                catch (MissingDataException e)
                {
                    // if there is missing data once blockchain processing has started, return the current progress
                    missingData.Add(e);
                    break;
                }
                catch (AggregateException e)
                {
                    if (e.InnerExceptions.Any(x => !(x is MissingDataException)))
                    {
                        throw;
                    }
                    else
                    {
                        missingData.AddRange(e.InnerExceptions.OfType<MissingDataException>());
                        break;
                    }
                }
            }

            LogBlockchainProgress(newBlockchain, totalStopwatch, totalTxCount, totalInputCount, currentRateStopwatch, currentBlockCount, currentTxCount, currentInputCount);

            return newBlockchain;
        }

        public Data.Blockchain RollbackBlockchain(Data.Blockchain blockchain, Block block)
        {
            List<TxOutputKey> spendOutputs, receiveOutputs;
            return RollbackBlockchain(blockchain, block, out spendOutputs, out receiveOutputs);
        }

        public Data.Blockchain RollbackBlockchain(Data.Blockchain blockchain, Block block, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
        {
            if (blockchain.BlockCount == 0 || blockchain.RootBlockHash != block.Hash)
                throw new ValidationException();

            var newUtxo = RollbackUtxo(blockchain, block, out spendOutputs, out receiveOutputs);

            return new Data.Blockchain
            (
                blockList: blockchain.BlockList.RemoveAt(blockchain.BlockCount - 1),
                blockListHashes: blockchain.BlockListHashes.Remove(blockchain.RootBlockHash),
                utxo: newUtxo
            );
        }

        private void LogBlockchainProgress(Data.Blockchain blockchain, Stopwatch totalStopwatch, long totalTxCount, long totalInputCount, Stopwatch currentRateStopwatch, long currentBlockCount, long currentTxCount, long currentInputCount)
        {
            var currentBlockRate = (float)currentBlockCount / currentRateStopwatch.EllapsedSecondsFloat();
            var currentTxRate = (float)currentTxCount / currentRateStopwatch.EllapsedSecondsFloat();
            var currentInputRate = (float)currentInputCount / currentRateStopwatch.EllapsedSecondsFloat();

            Debug.WriteLine(
                string.Join("\n",
                    new string('-', 80),
                    "Height: {0,10} | Date: {1} | Duration: {7} hh:mm:ss | Validation: {8} hh:mm:ss | Blocks/s: {2,7} | Tx/s: {3,7} | Inputs/s: {4,7} | Total Tx: {5,7} | Total Inputs: {6,7} | Utxo Size: {9,7}",
                    "GC Memory:      {10,10:#,##0.00} MB",
                    "Process Memory: {11,10:#,##0.00} MB",
                    new string('-', 80)
                )
                .Format2
                (
                    blockchain.Height.ToString("#,##0"),
                    "",
                    currentBlockRate.ToString("#,##0"),
                    currentTxRate.ToString("#,##0"),
                    currentInputRate.ToString("#,##0"),
                    totalTxCount.ToString("#,##0"),
                    totalInputCount.ToString("#,##0"),
                    totalStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                    validateStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                    blockchain.Utxo.Count.ToString("#,##0"),
                    (float)GC.GetTotalMemory(false) / 1.MILLION(),
                    (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()
                ));
        }

        private ImmutableHashSet<TxOutputKey> CalculateUtxo(long blockHeight, Block block, ImmutableHashSet<TxOutputKey> currentUtxo, out long txCount, out long inputCount)
        {
            txCount = 0;
            inputCount = 0;

            // create builder for new utxo
            var newUtxoBuilder = currentUtxo.ToBuilder();

            // don't include genesis block coinbase in utxo
            if (blockHeight > 0)
            {
                //TODO apply real coinbase rule
                // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                var coinbaseTx = block.Transactions[0];

                // add the coinbase outputs to the utxo
                for (var outputIndex = 0; outputIndex < coinbaseTx.Outputs.Length; outputIndex++)
                {
                    var txOutputKey = new TxOutputKey(coinbaseTx.Hash, (UInt32)outputIndex);

                    // add transaction output to to the utxo
                    if (!newUtxoBuilder.Add(txOutputKey))
                    {
                        // duplicate transaction output
                        Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(blockHeight, block.Hash.ToHexNumberString()));
                        //Debugger.Break();
                        //TODO throw new Validation();

                        //TODO this needs to be tracked so that blocks can be rolled back accurately
                        //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    }
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

                    // remove the output from the utxo
                    if (!newUtxoBuilder.Remove(input.PreviousTxOutputKey))
                    {
                        // output wasn't present in utxo, invalid block
                        throw new ValidationException();
                    }
                }

                for (var outputIndex = 0; outputIndex < tx.Outputs.Length; outputIndex++)
                {
                    var output = tx.Outputs[outputIndex];

                    // add the output to the list to be added to the utxo
                    var txOutputKey = new TxOutputKey(tx.Hash, (UInt32)outputIndex);

                    // add transaction output to to the utxo
                    if (!newUtxoBuilder.Add(txOutputKey))
                    {
                        // duplicate transaction output
                        Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, outputIndex));
                        //Debugger.Break();
                        //TODO throw new Validation();

                        //TODO this needs to be tracked so that blocks can be rolled back accurately
                        //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    }
                }
            }

            // validation successful, return the new utxo
            return newUtxoBuilder.ToImmutable();
        }

        private ImmutableHashSet<TxOutputKey> RollbackUtxo(Data.Blockchain blockchain, Block block, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
        {
            var blockHeight = blockchain.Height;
            var currentUtxo = blockchain.Utxo;

            // create builder for prev utxo
            var prevUtxoBuilder = currentUtxo.ToBuilder();
            spendOutputs = new List<TxOutputKey>();
            receiveOutputs = new List<TxOutputKey>();

            //TODO apply real coinbase rule
            // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
            var coinbaseTx = block.Transactions[0];

            for (var outputIndex = 0; outputIndex < coinbaseTx.Outputs.Length; outputIndex++)
            {
                var txOutputKey = new TxOutputKey(coinbaseTx.Hash, (UInt32)outputIndex);
                if (blockHeight > 0)
                {
                    // remove new outputs from the rolled back utxo
                    if (prevUtxoBuilder.Remove(txOutputKey))
                    {
                        receiveOutputs.Add(txOutputKey);
                    }
                    else
                    {
                        // missing transaction output
                        Debug.WriteLine("Missing transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), 0, outputIndex));
                        Debugger.Break();
                        //TODO throw new Validation();

                        //TODO this needs to be tracked so that blocks can be rolled back accurately
                        //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    }
                }
            }

            for (var txIndex = block.Transactions.Length - 1; txIndex >= 1; txIndex--)
            {
                var tx = block.Transactions[txIndex];

                for (var outputIndex = tx.Outputs.Length - 1; outputIndex >= 0; outputIndex--)
                {
                    var output = tx.Outputs[outputIndex];
                    var txOutputKey = new TxOutputKey(tx.Hash, (UInt32)outputIndex);
                    //TODO what if a transaction wasn't added to the utxo because it already existed?
                    //TODO the block would still pass without adding the tx to its utxo, but here it would get rolled back
                    //TODO maybe a flag bit to track this?

                    // remove new outputs from the rolled back utxo
                    if (prevUtxoBuilder.Remove(txOutputKey))
                    {
                        receiveOutputs.Add(txOutputKey);
                    }
                    else
                    {
                        // missing transaction output
                        Debug.WriteLine("Missing transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, outputIndex));
                        Debugger.Break();
                        //TODO throw new Validation();

                        //TODO this needs to be tracked so that blocks can be rolled back accurately
                        //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    }
                }

                for (var inputIndex = tx.Inputs.Length - 1; inputIndex >= 0; inputIndex--)
                {
                    var input = tx.Inputs[inputIndex];

                    // add spent outputs back into the rolled back utxo
                    if (prevUtxoBuilder.Add(input.PreviousTxOutputKey))
                    {
                        spendOutputs.Add(input.PreviousTxOutputKey);
                    }
                    else
                    {
                        // missing transaction output
                        Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, tx {2}, input {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, inputIndex));
                        Debugger.Break();
                        //TODO throw new Validation();

                        //TODO this needs to be tracked so that blocks can be rolled back accurately
                        //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    }
                }
            }

            return prevUtxoBuilder.ToImmutable();
        }

        //public TxLocator FindTransaction(Blockchain blockchain, UInt256 txHash)
        //{
        //    //TODO blockchain.Utxo.trygetvalue
        //    using (var cancelToken = new CancellationTokenSource())
        //    using (var foundToken = new CancellationTokenSource())
        //    {
        //        var lookupTasks = new Task[blockchain.BlockList.Count];
        //        var foundTx = new ConcurrentBag<TxLocator>();

        //        var i = 0;
        //        foreach (var block in
        //            LookAheadMethods.ParallelLookupLookAhead<BlockMetadata, Block>(
        //                blockchain.BlockList.Reverse().ToList(),
        //                blockMetadata => this.CacheContext.GetBlock(blockMetadata.BlockHash),
        //                cancelToken.Token))
        //        {
        //            lookupTasks[i] = Task.Run(()=>
        //            {
        //                Parallel.ForEach(block.Transactions, (tx, loopState, txIndex) =>
        //                {
        //                    if (tx.Hash == txHash)
        //                    {
        //                        foundTx.Add(new TxLocator(tx.Hash, block.Hash, (UInt32)txIndex));
        //                        foundToken.Cancel();
        //                        loopState.Break();
        //                    }
        //                });
        //            });

        //            i++;
        //        }

        //        while (foundTx.Count == 0)
        //        {
        //            Task.WaitAll(lookupTasks, foundToken.Token);
        //        }

        //        if (foundTx.Count > 1)
        //            throw new ValidationException();
        //        else if (foundTx.Count == 1)
        //            return foundTx.Single();
        //        else
        //            throw new MissingDataException(DataType.Transaction, txHash);
        //    }

        //    return default(TxLocator);
        //}

        public void RevalidateBlockchain(Data.Blockchain blockchain, Block genesisBlock)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                //TODO delete corrupted data? could get stuck in a fail-loop on the winning chain otherwise

                // verify blockchain has blocks
                if (blockchain.BlockList.Count == 0)
                    throw new ValidationException();

                // verify genesis block hash
                if (blockchain.BlockList[0].BlockHash != genesisBlock.Hash)
                    throw new ValidationException();

                // get genesis block header
                var chainGenesisBlockHeader = this.CacheContext.GetBlockHeader(blockchain.BlockList[0].BlockHash);

                // verify genesis block header
                if (
                    genesisBlock.Header.Version != chainGenesisBlockHeader.Version
                    || genesisBlock.Header.PreviousBlock != chainGenesisBlockHeader.PreviousBlock
                    || genesisBlock.Header.MerkleRoot != chainGenesisBlockHeader.MerkleRoot
                    || genesisBlock.Header.Time != chainGenesisBlockHeader.Time
                    || genesisBlock.Header.Bits != chainGenesisBlockHeader.Bits
                    || genesisBlock.Header.Nonce != chainGenesisBlockHeader.Nonce
                    || genesisBlock.Hash != chainGenesisBlockHeader.Hash
                    || genesisBlock.Hash != CalculateHash(chainGenesisBlockHeader))
                {
                    throw new ValidationException();
                }

                // setup expected previous block hash value to verify each chain actually does link
                var expectedPreviousBlockHash = genesisBlock.Header.PreviousBlock;
                for (var height = 0; height < blockchain.BlockList.Count; height++)
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();

                    // get the current link in the chain
                    var blockMetadata = blockchain.BlockList[height];

                    // verify height
                    if (blockMetadata.Height != height)
                        throw new ValidationException();

                    // verify blockchain linking
                    if (blockMetadata.PreviousBlockHash != expectedPreviousBlockHash)
                        throw new ValidationException();

                    // verify block exists
                    var blockHeader = this.CacheContext.GetBlockHeader(blockMetadata.BlockHash);

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != blockMetadata.PreviousBlockHash)
                        throw new ValidationException();

                    // verify block header hash
                    if (CalculateHash(blockHeader) != blockMetadata.BlockHash)
                        throw new ValidationException();

                    // next block metadata should have the current metadata's hash as its previous hash value
                    expectedPreviousBlockHash = blockMetadata.BlockHash;
                }

                // all validation passed
            }
            finally
            {
                stopwatch.Stop();
                Debug.WriteLine("Blockchain revalidation: {0:#,##0.000000}s".Format2(stopwatch.EllapsedSecondsFloat()));
            }
        }

        public IEnumerable<Tuple<Block, BlockMetadata, ImmutableDictionary<UInt256, Transaction>>> BlockAndMetadataLookAhead(IList<UInt256> blockHashes, ImmutableHashSet<UInt256> blockchainHashes)
        {
            var blockLookAhead = LookAheadMethods.ParallelLookupLookAhead<UInt256, Tuple<Block, ImmutableDictionary<UInt256, Transaction>>>(
                blockHashes, blockHash =>
                {
                    var block = this.CacheContext.GetBlock(blockHash, saveInCache: false);

                    var transactionsBuilder = ImmutableDictionary.CreateBuilder<UInt256, Transaction>();
                    if (blockchainHashes != null)
                    {
                        var inputTxHashList = block.Transactions.Skip(1).SelectMany(x => x.Inputs).Select(x => x.PreviousTxOutputKey.TxHash).Distinct();

                        // pre-cache input transactions
                        foreach (var inputTxHash in inputTxHashList)
                        {
                            Transaction inputTx;
                            if (this.CacheContext.TransactionCache.TryGetValue(new TxKeySearch(inputTxHash, blockchainHashes), out inputTx, saveInCache: false))
                            {
                                transactionsBuilder.Add(inputTxHash, inputTx);
                            }
                        }
                    }

                    return Tuple.Create(block, transactionsBuilder.ToImmutable());
                }, this.shutdownToken);

            var metadataLookAhead = LookAheadMethods.ParallelLookupLookAhead<UInt256, BlockMetadata>(
                blockHashes, blockHash => this.CacheContext.GetBlockMetadata(blockHash, saveInCache: false), this.shutdownToken);

            return blockLookAhead.Zip(metadataLookAhead, (block, blockMetadata) => Tuple.Create(block.Item1, blockMetadata, block.Item2));
        }

        public IEnumerable<Tuple<BlockMetadata, Block>> PreviousBlocksLookAhead(BlockMetadata firstBlock)
        {
            using (var cancelToken = new CancellationTokenSource())
            {
                foreach (var tuple in LookAheadMethods.LookAhead(() => PreviousBlocks(firstBlock), cancelToken.Token))
                {
                    yield return tuple;
                }
            }
        }


        public IEnumerable<Tuple<BlockMetadata, Block>> PreviousBlocks(BlockMetadata firstBlock)
        {
            var prevBlockMetadata = firstBlock;
            //TODO some kind of hard stop
            while (true)
            {
                var prevBlock = this.CacheContext.GetBlock(prevBlockMetadata.BlockHash);

                yield return Tuple.Create(prevBlockMetadata, prevBlock);

                var prevBlockHash = prevBlockMetadata.PreviousBlockHash;
                if (prevBlockHash == 0)
                {
                    break;
                }

                prevBlockMetadata = this.CacheContext.GetBlockMetadata(prevBlockHash);
            }
        }

        public IEnumerable<BlockMetadata> PreviousBlockMetadata(BlockMetadata firstBlock)
        {
            var prevBlockMetadata = firstBlock;
            //TODO some kind of hard stop
            while (true)
            {
                yield return prevBlockMetadata;

                var prevBlockHash = prevBlockMetadata.PreviousBlockHash;
                if (prevBlockHash == 0)
                {
                    break;
                }

                prevBlockMetadata = this.CacheContext.GetBlockMetadata(prevBlockHash);
            }
        }

        private UInt256 CalculateHash(BlockHeader blockHeader)
        {
            return new UInt256(Crypto.DoubleSHA256(WireEncoder.EncodeBlockHeader(blockHeader)));
        }
    }
}
