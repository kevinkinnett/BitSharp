using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain.ExtensionMethods;
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
using System.Globalization;

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

        public Data.Blockchain CalculateBlockchainFromExisting(Data.Blockchain currentBlockchain, ChainedBlock targetChainedBlock, out List<MissingDataException> missingData, CancellationToken cancelToken, Action<Data.Blockchain> onProgress = null)
        {
            Debug.WriteLine("Winning chained block {0} at height {1}, total work: {2}".Format2(targetChainedBlock.BlockHash.ToHexNumberString(), targetChainedBlock.Height, targetChainedBlock.TotalWork.ToString("X")));

            // if the target block is at height 0 don't use currentBlockchain as-is, set it to be the genesis chain for the target block
            if (targetChainedBlock.Height == 0)
            {
                currentBlockchain = new Data.Blockchain
                (
                    blockList: ImmutableList.Create(targetChainedBlock),
                    blockListHashes: ImmutableHashSet.Create(targetChainedBlock.BlockHash),
                    utxo: ImmutableDictionary.Create<UInt256, UnspentTx>()
                );
            }
            // if currentBlockchain is not present find the genesis block for the target block and use it as the current chain
            else if (currentBlockchain.IsDefault)
            {
                // find the genesis block for the target block
                var genesisBlock = targetChainedBlock;
                foreach (var prevBlock in PreviousChainedBlocks(targetChainedBlock))
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    genesisBlock = prevBlock;
                }

                currentBlockchain = new Data.Blockchain
                (
                    blockList: ImmutableList.Create(genesisBlock),
                    blockListHashes: ImmutableHashSet.Create(genesisBlock.BlockHash),
                    utxo: ImmutableDictionary.Create<UInt256, UnspentTx>()
                );
            }

            missingData = new List<MissingDataException>();

            Debug.WriteLine("Searching for last common ancestor between current chainblock and winning chainblock");

            List<UInt256> newChainBlockList;
            var lastCommonAncestorChain = RollbackToLastCommonAncestor(currentBlockchain, targetChainedBlock, cancelToken, out newChainBlockList);

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

            // with last common ancestor found and utxo rolled back to that point, calculate the new blockchain
            // use ImmutableList for BlockList during modification
            var newBlockchain = new Data.Blockchain
            (
                blockList: lastCommonAncestorChain.BlockList,
                blockListHashes: lastCommonAncestorChain.BlockListHashes,
                utxo: lastCommonAncestorChain.Utxo
            );

            // start calculating new utxo
            foreach (var tuple in BlockAndTxLookAhead(newChainBlockList))
            {
                // cooperative loop
                this.shutdownToken.ThrowIfCancellationRequested();
                cancelToken.ThrowIfCancellationRequested();

                try
                {
                    // get block and metadata for next link in blockchain
                    var nextBlock = tuple.Item1;
                    var nextChainedBlock = tuple.Item2;

                    // calculate the new block utxo, double spends will be checked for
                    ImmutableDictionary<UInt256, ImmutableHashSet<int>> newTransactions = ImmutableDictionary.Create<UInt256, ImmutableHashSet<int>>();
                    long txCount = 0, inputCount = 0;
                    var newUtxo = new MethodTimer(false).Time("CalculateUtxo", () =>
                        CalculateUtxo(nextChainedBlock.Height, nextBlock, newBlockchain.Utxo, out newTransactions, out txCount, out inputCount));

                    var nextBlockchain =
                        new MethodTimer(false).Time("nextBlockchain", () =>
                            new Data.Blockchain
                            (
                                blockList: newBlockchain.BlockList.Add(nextChainedBlock),
                                blockListHashes: newBlockchain.BlockListHashes.Add(nextChainedBlock.BlockHash),
                                utxo: newUtxo
                            ));

                    // validate the block
                    // validation utxo includes all transactions added in the same block, any double spends will have failed the block above
                    validateStopwatch.Start();
                    new MethodTimer(false).Time("ValidateBlock", () =>
                        this.Rules.ValidateBlock(nextBlock, nextBlockchain, newBlockchain.Utxo, newTransactions));
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

            if (onProgress != null)
                onProgress(newBlockchain);

            LogBlockchainProgress(newBlockchain, totalStopwatch, totalTxCount, totalInputCount, currentRateStopwatch, currentBlockCount, currentTxCount, currentInputCount);

            return newBlockchain;
        }

        public Data.Blockchain RollbackToLastCommonAncestor(Data.Blockchain currentBlockchain, ChainedBlock targetChainedBlock, CancellationToken cancelToken, out List<UInt256> newChainBlockList)
        {
            // take snapshots
            var newChainedBlock = targetChainedBlock;
            newChainBlockList = new List<UInt256>();

            // check height difference between chains, they will be roll backed before checking for the last common ancestor
            var heightDelta = targetChainedBlock.Height - currentBlockchain.Height;

            // if current chain is shorter, roll new chain back to current chain's height
            if (heightDelta > 0)
            {
                List<ChainedBlock> rolledBackChainedBlocks;
                newChainedBlock = RollbackChainedBlockToHeight(targetChainedBlock, currentBlockchain.Height, out rolledBackChainedBlocks, this.shutdownToken);
                newChainBlockList.AddRange(rolledBackChainedBlocks.Select(x => x.BlockHash));
            }
            // if current chain is longer, roll it back to new chain's height
            else if (heightDelta < 0)
            {
                List<Data.Blockchain> rolledBackBlockchains;
                currentBlockchain = RollbackBlockchainToHeight(currentBlockchain, newChainedBlock.Height, out rolledBackBlockchains, this.shutdownToken);
            }

            if (newChainedBlock.Height != currentBlockchain.Height)
                throw new Exception();

            //TODO continue looking backwards while processing moves forward to double check
            //TODO the blockchain history back to genesis? only look at height, work, valid bits in
            //TODO the metadata, sync and check this task at the end before updating current blockchain,
            //TODO if any error is ever found, mark everything after it as invalid or unprocessed, the
            //TODO processor could get stuck otherwise trying what it thinks is the winning chain over and over

            // with both chains at the same height, roll back to last common ancestor
            if (newChainedBlock.BlockHash != currentBlockchain.RootBlockHash)
            {
                var rollbackList = new List<UInt256>();
                var currentBlockchainIndex = currentBlockchain.BlockList.Count - 1;
                foreach (var prevBlock in PreviousChainedBlocks(newChainedBlock))
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    newChainedBlock = prevBlock;
                    if (newChainedBlock.BlockHash == currentBlockchain.BlockList[currentBlockchainIndex].BlockHash)
                    {
                        break;
                    }

                    // ensure that height is as expected while looking up previous blocks
                    if (newChainedBlock.Height != currentBlockchain.BlockList[currentBlockchainIndex].Height)
                    {
                        throw new ValidationException();
                    }

                    // keep track of rolled back data on the new blockchain
                    newChainBlockList.Add(newChainedBlock.BlockHash);

                    // queue up current blockchain rollback
                    rollbackList.Add(currentBlockchain.BlockList[currentBlockchainIndex].BlockHash);

                    currentBlockchainIndex--;
                }

                // roll back current block chain
                foreach (var tuple in BlockLookAhead(rollbackList))
                {
                    var block = tuple.Item1;
                    currentBlockchain = RollbackBlockchain(currentBlockchain, block);
                }
            }

            // work list will have last items added first, reverse
            newChainBlockList.Reverse();

            return currentBlockchain;
        }

        public List<UInt256> FindBlocksPastLastCommonAncestor(Data.Blockchain currentBlockchain, ChainedBlock targetChainedBlock, CancellationToken cancelToken)
        {
            return new MethodTimer().Time(() =>
            {
                // take snapshots
                var newChainedBlock = targetChainedBlock;
                var newChainBlockList = new List<UInt256>();

                // check height difference between chains, they will be roll backed before checking for the last common ancestor
                var heightDelta = targetChainedBlock.Height - currentBlockchain.Height;

                // if current chain is shorter, roll new chain back to current chain's height
                ImmutableList<ChainedBlock> currentChainedBlocks;
                if (heightDelta > 0)
                {
                    currentChainedBlocks = currentBlockchain.BlockList;

                    List<ChainedBlock> rolledBackChainedBlocks;
                    newChainedBlock = RollbackChainedBlockToHeight(targetChainedBlock, currentBlockchain.Height, out rolledBackChainedBlocks, this.shutdownToken);
                    newChainBlockList.AddRange(rolledBackChainedBlocks.Select(x => x.BlockHash));
                }
                // if current chain is longer, roll it back to new chain's height
                else if (heightDelta < 0)
                {
                    currentChainedBlocks = currentBlockchain.BlockList.GetRange(0, targetChainedBlock.Height + 1);
                }
                else
                {
                    currentChainedBlocks = currentBlockchain.BlockList;
                }

                if (newChainedBlock.Height != currentChainedBlocks.Last().Height)
                    throw new Exception();

                // with both chains at the same height, roll back to last common ancestor
                if (newChainedBlock.BlockHash != currentChainedBlocks.Last().BlockHash)
                {
                    foreach (var tuple in
                        PreviousChainedBlocks(newChainedBlock).Zip(currentChainedBlocks.Reverse<ChainedBlock>(),
                            (prevBlock, currentBlock) => Tuple.Create(prevBlock, currentBlock)))
                    {
                        // cooperative loop
                        this.shutdownToken.ThrowIfCancellationRequested();
                        cancelToken.ThrowIfCancellationRequested();

                        newChainedBlock = tuple.Item1;
                        var currentBlock = tuple.Item2;

                        // ensure that height is as expected while looking up previous blocks
                        if (newChainedBlock.Height != currentBlock.Height)
                        {
                            throw new ValidationException();
                        }

                        if (newChainedBlock.BlockHash == currentBlock.BlockHash)
                        {
                            break;
                        }

                        // keep track of rolled back data on the new blockchain
                        newChainBlockList.Add(newChainedBlock.BlockHash);
                    }
                }

                // work list will have last items added first, reverse
                newChainBlockList.Reverse();

                return newChainBlockList;
            });
        }

        public ChainedBlock RollbackChainedBlockToHeight(ChainedBlock chainedBlock, int targetHeight, out List<ChainedBlock> rolledBackChainedBlocks, CancellationToken cancelToken)
        {
            if (targetHeight > chainedBlock.Height || targetHeight < 0)
                throw new ArgumentOutOfRangeException("targetHeight");

            rolledBackChainedBlocks = new List<ChainedBlock>();

            var targetChainedBlock = chainedBlock;
            var expectedHeight = targetChainedBlock.Height;
            while (targetChainedBlock.Height > targetHeight)
            {
                // cooperative loop
                cancelToken.ThrowIfCancellationRequested();

                // keep track of rolled back data on the new blockchain
                rolledBackChainedBlocks.Add(targetChainedBlock);

                // roll back
                targetChainedBlock = this.CacheContext.GetChainedBlock(targetChainedBlock.PreviousBlockHash);

                // ensure that height is as expected while looking up previous blocks
                expectedHeight--;
                if (targetChainedBlock.Height != expectedHeight)
                    throw new ValidationException();
            }

            return targetChainedBlock;
        }

        public Data.Blockchain RollbackBlockchainToHeight(Data.Blockchain blockchain, int targetHeight, out List<Data.Blockchain> rolledBackBlockchains, CancellationToken cancelToken)
        {
            if (targetHeight > blockchain.Height || targetHeight < 0)
                throw new ArgumentOutOfRangeException("targetHeight");

            List<ChainedBlock> rolledBackChainedBlocks;
            var targetChainedBlock = RollbackChainedBlockToHeight(blockchain.RootBlock, targetHeight, out rolledBackChainedBlocks, cancelToken);

            var rollbackCount = blockchain.Height - targetHeight;
            if (rolledBackChainedBlocks.Count != rollbackCount)
                throw new Exception();

            rolledBackBlockchains = new List<Data.Blockchain>();

            var targetBlockchain = blockchain;
            var rollbackIndex = 0;
            foreach (var tuple in BlockLookAhead(rolledBackChainedBlocks.Select(x => x.BlockHash).ToList()))
            {
                // cooperative loop
                this.shutdownToken.ThrowIfCancellationRequested();
                cancelToken.ThrowIfCancellationRequested();

                // keep track of rolled back data on the new blockchain
                rolledBackBlockchains.Add(targetBlockchain);

                // roll back
                var block = tuple.Item1;
                Debug.Assert(targetBlockchain.RootBlockHash == block.Hash);
                targetBlockchain = RollbackBlockchain(targetBlockchain, block);

                Debug.WriteLineIf(rollbackIndex % 100 == 0, "Rolling back {0} of {1}".Format2(rollbackIndex + 1, rollbackCount));
                rollbackIndex++;
            }

            return targetBlockchain;
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
            var currentBlockRate = (float)currentBlockCount / currentRateStopwatch.ElapsedSecondsFloat();
            var currentTxRate = (float)currentTxCount / currentRateStopwatch.ElapsedSecondsFloat();
            var currentInputRate = (float)currentInputCount / currentRateStopwatch.ElapsedSecondsFloat();

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

        private ImmutableDictionary<UInt256, UnspentTx> CalculateUtxo(long blockHeight, Block block, ImmutableDictionary<UInt256, UnspentTx> currentUtxo, out ImmutableDictionary<UInt256, ImmutableHashSet<int>> newTransactions, out long txCount, out long inputCount)
        {
            txCount = 0;
            inputCount = 0;

            // create builder for new utxo
            var newUtxoBuilder = currentUtxo.ToBuilder();
            var newTransactionsBuilder = ImmutableDictionary.CreateBuilder<UInt256, ImmutableHashSet<int>>();

            // don't include genesis block coinbase in utxo
            if (blockHeight > 0)
            {
                //TODO apply real coinbase rule
                // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                var coinbaseTx = block.Transactions[0];

                // add the coinbase outputs to the utxo
                var coinbaseUnspentTx = new UnspentTx(block.Hash, 0, coinbaseTx.Hash, new ImmutableBitArray(coinbaseTx.Outputs.Length, true));

                // add transaction output to to the utxo
                if (newUtxoBuilder.ContainsKey(coinbaseTx.Hash))
                {
                    // duplicate transaction output
                    Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(blockHeight, block.Hash.ToHexNumberString()));

                    if ((blockHeight == 91842 && coinbaseTx.Hash == UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber))
                        || (blockHeight == 91880 && coinbaseTx.Hash == UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber)))
                    {
                        newUtxoBuilder.Remove(coinbaseTx.Hash);
                    }
                    else
                    {
                        throw new ValidationException();
                    }
                }

                newTransactionsBuilder.Add(coinbaseTx.Hash, ImmutableHashSet.Create(0));
                newUtxoBuilder.Add(coinbaseTx.Hash, coinbaseUnspentTx);
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

                    if (!newUtxoBuilder.ContainsKey(input.PreviousTxOutputKey.TxHash))
                    {
                        // output wasn't present in utxo, invalid block
                        throw new ValidationException();
                    }

                    var prevUnspentTx = newUtxoBuilder[input.PreviousTxOutputKey.TxHash];

                    if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.UnspentOutputs.Length)
                    {
                        // output was out of bounds
                        throw new ValidationException();
                    }

                    if (!prevUnspentTx.UnspentOutputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()])
                    {                        // output was already spent
                        throw new ValidationException();
                    }


                    // remove the output from the utxo
                    newUtxoBuilder[input.PreviousTxOutputKey.TxHash] =
                        new UnspentTx(prevUnspentTx.BlockHash, prevUnspentTx.TxIndex, prevUnspentTx.TxHash,
                            prevUnspentTx.UnspentOutputs.Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), false));

                    // remove fully spent transaction from the utxo
                    if (newUtxoBuilder[input.PreviousTxOutputKey.TxHash].UnspentOutputs.All(x => !x))
                        newUtxoBuilder.Remove(input.PreviousTxOutputKey.TxHash);
                }

                // add the output to the list to be added to the utxo
                var unspentTx = new UnspentTx(block.Hash, (UInt32)txIndex, tx.Hash, new ImmutableBitArray(tx.Outputs.Length, true));

                // add transaction output to to the utxo
                if (newUtxoBuilder.ContainsKey(tx.Hash))
                {
                    // duplicate transaction output
                    Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, tx {2}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex));
                    //Debugger.Break();
                    //TODO throw new Validation();

                    //TODO this needs to be tracked so that blocks can be rolled back accurately
                    //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                }

                if (!newTransactionsBuilder.ContainsKey(tx.Hash))
                    newTransactionsBuilder.Add(tx.Hash, ImmutableHashSet.Create(txIndex));
                else
                    newTransactionsBuilder[tx.Hash] = newTransactionsBuilder[tx.Hash].Add(txIndex);
                newUtxoBuilder.Add(tx.Hash, unspentTx);
            }

            // validation successful, return the new utxo
            newTransactions = newTransactionsBuilder.ToImmutable();
            return newUtxoBuilder.ToImmutable();
        }

        private ImmutableDictionary<UInt256, UnspentTx> RollbackUtxo(Data.Blockchain blockchain, Block block, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
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
                    if (prevUtxoBuilder.Remove(coinbaseTx.Hash))
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
                    if (prevUtxoBuilder.Remove(tx.Hash))
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
                    if (prevUtxoBuilder.ContainsKey(input.PreviousTxOutputKey.TxHash))
                    {
                        var prevUnspentTx = prevUtxoBuilder[input.PreviousTxOutputKey.TxHash];

                        // check if output is out of bounds
                        if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.UnspentOutputs.Length)
                            throw new ValidationException();

                        // check that output isn't already considered unspent
                        if (prevUnspentTx.UnspentOutputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()])
                            throw new ValidationException();

                        // mark output as unspent
                        prevUtxoBuilder[input.PreviousTxOutputKey.TxHash] =
                            new UnspentTx(prevUnspentTx.BlockHash, prevUnspentTx.TxIndex, prevUnspentTx.TxHash,
                                prevUnspentTx.UnspentOutputs.Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), true));
                    }
                    else
                    {
                        // fully spent transaction being added back in during roll back
                        //TODO
                        throw new NotImplementedException();
                    }

                    //TODO
                    //if (prevUtxoBuilder.Add(input.PreviousTxOutputKey))
                    //{
                    //    spendOutputs.Add(input.PreviousTxOutputKey);
                    //}
                    //else
                    //{
                    //    // missing transaction output
                    //    Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, tx {2}, input {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, inputIndex));
                    //    Debugger.Break();
                    //    //TODO throw new Validation();

                    //    //TODO this needs to be tracked so that blocks can be rolled back accurately
                    //    //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    //}
                }
            }

            return prevUtxoBuilder.ToImmutable();
        }

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
                    var chainedBlock = blockchain.BlockList[height];

                    // verify height
                    if (chainedBlock.Height != height)
                        throw new ValidationException();

                    // verify blockchain linking
                    if (chainedBlock.PreviousBlockHash != expectedPreviousBlockHash)
                        throw new ValidationException();

                    // verify block exists
                    var blockHeader = this.CacheContext.GetBlockHeader(chainedBlock.BlockHash);

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != chainedBlock.PreviousBlockHash)
                        throw new ValidationException();

                    // verify block header hash
                    if (CalculateHash(blockHeader) != chainedBlock.BlockHash)
                        throw new ValidationException();

                    // next block metadata should have the current metadata's hash as its previous hash value
                    expectedPreviousBlockHash = chainedBlock.BlockHash;
                }

                // all validation passed
            }
            finally
            {
                stopwatch.Stop();
                Debug.WriteLine("Blockchain revalidation: {0:#,##0.000000}s".Format2(stopwatch.ElapsedSecondsFloat()));
            }
        }

        public IEnumerable<Tuple<Block, ChainedBlock>> BlockLookAhead(IList<UInt256> blockHashes)
        {
            var blockLookAhead = LookAheadMethods.LookAhead(
                () => blockHashes.Select(blockHash => this.CacheContext.GetBlock(blockHash, saveInCache: false)),
                this.shutdownToken);

            var chainedBlockLookAhead = LookAheadMethods.LookAhead(
                () => blockHashes.Select(blockHash => this.CacheContext.GetChainedBlock(blockHash, saveInCache: false)),
                this.shutdownToken);

            return blockLookAhead.Zip(chainedBlockLookAhead, (block, chainedBlock) => Tuple.Create(block, chainedBlock));
        }

        public IEnumerable<Tuple<Block, ChainedBlock /*, ImmutableDictionary<UInt256, Transaction>*/>> BlockAndTxLookAhead(IList<UInt256> blockHashes)
        {
            var blockLookAhead = LookAheadMethods.LookAhead(
                () => blockHashes.Select(
                    blockHash =>
                    {
                        var block = new MethodTimer(false).Time("GetBlock", () =>
                            this.CacheContext.GetBlock(blockHash, saveInCache: false));

                        this.CacheContext.TransactionCache.CacheBlock(block);

                        //var transactionsBuilder = ImmutableDictionary.CreateBuilder<UInt256, Transaction>();
                        //var inputTxHashList = block.Transactions.Skip(1).SelectMany(x => x.Inputs).Select(x => x.PreviousTxOutputKey.TxHash).Distinct();

                        //// pre-cache input transactions
                        ////Parallel.ForEach(inputTxHashList, inputTxHash =>
                        //foreach (var inputTxHash in inputTxHashList)
                        //{
                        //    Transaction inputTx;
                        //    if (this.CacheContext.TransactionCache.TryGetValue(inputTxHash, out inputTx, saveInCache: false))
                        //    {
                        //        transactionsBuilder.Add(inputTxHash, inputTx);
                        //    }
                        //}

                        //return Tuple.Create(block, transactionsBuilder.ToImmutable());
                        return block;
                    }),
                this.shutdownToken);

            var chainedBlockLookAhead = LookAheadMethods.LookAhead(
                () => blockHashes.Select(blockHash => this.CacheContext.GetChainedBlock(blockHash, saveInCache: false)),
                this.shutdownToken);

            return blockLookAhead.Zip(chainedBlockLookAhead, (block, chainedBlock) => Tuple.Create(block, chainedBlock));
        }

        public IEnumerable<Tuple<ChainedBlock, Block>> PreviousBlocksLookAhead(ChainedBlock firstBlock)
        {
            using (var cancelToken = new CancellationTokenSource())
            {
                foreach (var tuple in LookAheadMethods.LookAhead(() => PreviousBlocks(firstBlock), cancelToken.Token))
                {
                    yield return tuple;
                }
            }
        }


        public IEnumerable<Tuple<ChainedBlock, Block>> PreviousBlocks(ChainedBlock firstBlock)
        {
            var prevChainedBlock = firstBlock;
            //TODO some kind of hard stop
            while (true)
            {
                var prevBlock = this.CacheContext.GetBlock(prevChainedBlock.BlockHash);

                yield return Tuple.Create(prevChainedBlock, prevBlock);

                var prevBlockHash = prevChainedBlock.PreviousBlockHash;
                if (prevBlockHash == 0)
                {
                    break;
                }

                prevChainedBlock = this.CacheContext.GetChainedBlock(prevBlockHash);
            }
        }

        public IEnumerable<ChainedBlock> PreviousChainedBlocks(ChainedBlock firstBlock)
        {
            var prevChainedBlock = firstBlock;
            //TODO some kind of hard stop
            while (true)
            {
                yield return prevChainedBlock;

                var prevBlockHash = prevChainedBlock.PreviousBlockHash;
                if (prevBlockHash == 0)
                {
                    break;
                }

                prevChainedBlock = this.CacheContext.GetChainedBlock(prevBlockHash);
            }
        }

        private UInt256 CalculateHash(BlockHeader blockHeader)
        {
            return new UInt256(Crypto.DoubleSHA256(DataCalculator.EncodeBlockHeader(blockHeader)));
        }
    }
}
