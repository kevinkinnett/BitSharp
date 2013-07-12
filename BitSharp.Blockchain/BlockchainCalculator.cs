using BitSharp.Blockchain.ExtensionMethods;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
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

namespace BitSharp.Blockchain
{
    public class BlockchainCalculator
    {
        private readonly IBlockchainRules rules;
        private readonly IBlockchainRetriever retriever;
        private readonly CancellationToken shutdownToken;
        //TODO
        private readonly Stopwatch validateStopwatch = new Stopwatch();

        public BlockchainCalculator(IBlockchainRules rules, IBlockchainRetriever retriever, CancellationToken shutdownToken)
        {
            this.rules = rules;
            this.retriever = retriever;
            this.shutdownToken = shutdownToken;
        }

        public Blockchain CalculateBlockchainFromExisting(Blockchain currentBlockchain, BlockMetadata targetBlockMetadata, CancellationToken cancelToken, Action<Blockchain> onProgress = null)
        {
            Debug.WriteLine("Winning chained block {0} at height {1}, total work: {2}".Format2(targetBlockMetadata.BlockHash.ToHexNumberString(), targetBlockMetadata.Height.Value, targetBlockMetadata.TotalWork.Value.ToString("X")));

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

                    newChainBlockMetadata = this.retriever.GetBlockMetadata(newChainBlockMetadata.PreviousBlockHash);

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

                for (var i = 0; i < -heightDelta; i++)
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    currentBlockchain = RollbackBlockchain(currentBlockchain);
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
            while (newChainBlockMetadata.BlockHash != currentBlockchain.RootBlockHash)
            {
                // cooperative loop
                this.shutdownToken.ThrowIfCancellationRequested();
                cancelToken.ThrowIfCancellationRequested();

                // roll back current block chain
                currentBlockchain = RollbackBlockchain(currentBlockchain);

                // roll back new block chain
                newChainBlockMetadata = this.retriever.GetBlockMetadata(newChainBlockMetadata.PreviousBlockHash);

                // ensure that height is as expected while looking up previous blocks
                if (newChainBlockMetadata.Height != currentBlockchain.Height)
                {
                    throw new ValidationException();
                }

                // keep track of rolled back data on the new blockchain
                newChainBlockList.Add(newChainBlockMetadata.BlockHash);
            }

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
            var newBlockchain = new Blockchain
            (
                blockList: currentBlockchain.BlockList,
                utxo: currentBlockchain.Utxo
            );

            // start calculating new utxo
            foreach (var tuple in BlockAndMetadataLookAhead(newChainBlockList))
            {
                // cooperative loop
                this.shutdownToken.ThrowIfCancellationRequested();
                cancelToken.ThrowIfCancellationRequested();

                try
                {
                    // get block and metadata for next link in blockchain
                    var nextBlock = tuple.Item1;
                    var nextBlockMetadata = tuple.Item2;

                    if (nextBlockMetadata.Height == null || nextBlockMetadata.TotalWork == null)
                        throw new ValidationException();

                    // calculate the new block utxo, double spends will be checked for
                    long txCount, inputCount;
                    var newUtxo = CalculateUtxo(nextBlockMetadata.Height.Value, nextBlock, newBlockchain.Utxo, out txCount, out inputCount);

                    var nextBlockchain =
                        new Blockchain
                        (
                            blockList: newBlockchain.BlockList.Add(nextBlockMetadata),
                            utxo: newUtxo
                        );

                    //TODO rewrite transactions
                    //Parallel.ForEach(nextBlock.Transactions, tx =>
                    //    this.storageManager.txStorage.CreateValue(tx.Hash, tx));

                    // validate the block
                    // validation utxo includes all transactions added in the same block, any double spends will have failed the block above
                    validateStopwatch.Start();
                    this.rules.ValidateBlock(nextBlock, nextBlockchain, this.retriever);
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
                    break;
                }
            }

            LogBlockchainProgress(newBlockchain, totalStopwatch, totalTxCount, totalInputCount, currentRateStopwatch, currentBlockCount, currentTxCount, currentInputCount);

            return newBlockchain;
        }

        public Blockchain RollbackBlockchain(Blockchain blockchain)
        {
            List<TxOutputKey> spendOutputs, receiveOutputs;
            return RollbackBlockchain(blockchain, out spendOutputs, out receiveOutputs);
        }

        public Blockchain RollbackBlockchain(Blockchain blockchain, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
        {
            if (blockchain.BlockCount == 0)
                throw new ValidationException();

            var block = this.retriever.GetBlock(blockchain.RootBlockHash);

            var newUtxo = RollbackUtxo(blockchain.Height, block, blockchain.Utxo, out spendOutputs, out receiveOutputs);

            return new Blockchain
            (
                blockList: blockchain.BlockList.RemoveAt(blockchain.BlockCount - 1),
                utxo: newUtxo
            );
        }

        private void LogBlockchainProgress(Blockchain blockchain, Stopwatch totalStopwatch, long totalTxCount, long totalInputCount, Stopwatch currentRateStopwatch, long currentBlockCount, long currentTxCount, long currentInputCount)
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
                    var txOutputKey = new TxOutputKey(coinbaseTx.Hash, outputIndex);

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

                    // get the previous transaction output key
                    var prevTxOutputKey = new TxOutputKey(input.PreviousTransactionHash, input.PreviousTransactionIndex.ToIntChecked());

                    // remove the output from the utxo
                    if (!newUtxoBuilder.Remove(prevTxOutputKey))
                    {
                        // output wasn't present in utxo, invalid block
                        throw new ValidationException();
                    }
                }

                for (var outputIndex = 0; outputIndex < tx.Outputs.Length; outputIndex++)
                {
                    var output = tx.Outputs[outputIndex];

                    // add the output to the list to be added to the utxo
                    var txOutputKey = new TxOutputKey(tx.Hash, outputIndex);

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

        private ImmutableHashSet<TxOutputKey> RollbackUtxo(long blockHeight, Block block, ImmutableHashSet<TxOutputKey> currentUtxo, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
        {
            // create builder for prev utxo
            var prevUtxoBuilder = currentUtxo.ToBuilder();
            spendOutputs = new List<TxOutputKey>();
            receiveOutputs = new List<TxOutputKey>();

            //TODO apply real coinbase rule
            // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
            var coinbaseTx = block.Transactions[0];

            for (var outputIndex = 0; outputIndex < coinbaseTx.Outputs.Length; outputIndex++)
            {
                var txOutputKey = new TxOutputKey(coinbaseTx.Hash, outputIndex);
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
                    var txOutputKey = new TxOutputKey(tx.Hash, outputIndex);
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
                    var prevTxOutputKey = new TxOutputKey(input.PreviousTransactionHash, input.PreviousTransactionIndex.ToIntChecked());

                    // add spent outputs back into the rolled back utxo
                    if (prevUtxoBuilder.Add(prevTxOutputKey))
                    {
                        spendOutputs.Add(prevTxOutputKey);
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

        public void RevalidateBlockchain(Blockchain blockchain, Block genesisBlock)
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
                var chainGenesisBlockHeader = this.retriever.GetBlockHeader(blockchain.BlockList[0].BlockHash);

                // verify genesis block header
                if (
                    genesisBlock.Header.Version != chainGenesisBlockHeader.Version
                    || genesisBlock.Header.PreviousBlock != chainGenesisBlockHeader.PreviousBlock
                    || genesisBlock.Header.MerkleRoot != chainGenesisBlockHeader.MerkleRoot
                    || genesisBlock.Header.Time != chainGenesisBlockHeader.Time
                    || genesisBlock.Header.Bits != chainGenesisBlockHeader.Bits
                    || genesisBlock.Header.Nonce != chainGenesisBlockHeader.Nonce
                    || genesisBlock.Hash != chainGenesisBlockHeader.Hash
                    || genesisBlock.Hash != new UInt256(Crypto.DoubleSHA256(chainGenesisBlockHeader.ToRawBytes())))
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
                    var blockHeader = this.retriever.GetBlockHeader(blockMetadata.BlockHash);

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != blockMetadata.PreviousBlockHash)
                        throw new ValidationException();

                    // verify block header hash
                    if (new UInt256(Crypto.DoubleSHA256(blockHeader.ToRawBytes())) != blockMetadata.BlockHash)
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

        public IEnumerable<Tuple<Block, BlockMetadata>> BlockAndMetadataLookAhead(IList<UInt256> blockHashes)
        {
            var blockLookAhead = LookAheadMethods.ParallelLookupLookAhead<UInt256, Block>(blockHashes, blockHash => this.retriever.GetBlock(blockHash, saveInCache: false), this.shutdownToken);
            var metadataLookAhead = LookAheadMethods.ParallelLookupLookAhead<UInt256, BlockMetadata>(blockHashes, blockHash => this.retriever.GetBlockMetadata(blockHash, saveInCache: false), this.shutdownToken);

            return blockLookAhead.Zip(metadataLookAhead, (block, blockMetadata) => Tuple.Create(block, blockMetadata));
        }
    }
}
