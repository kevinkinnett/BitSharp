using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using BitSharp.Storage.Test;
using BitSharp.Transactions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    public class MemoryBlockchain
    {
        private const UInt64 SATOSHI_PER_BTC = 100 * 1000 * 1000;

        private readonly CancellationToken shutdownToken;
        private readonly Random random;

        private readonly MemoryStorageContext _storageContext;
        private readonly CacheContext _cacheContext;

        private readonly Block _genesisBlock;
        private readonly ChainedBlock _genesisChainedBlock;

        private readonly UnitTestRules _rules;
        private readonly BlockchainCalculator _calculator;

        private Data.Blockchain _currentBlockchain;

        private readonly ECPrivateKeyParameters _coinbasePrivateKey;
        private readonly ECPublicKeyParameters _coinbasePublicKey;

        public MemoryBlockchain(Block? genesisBlock = null)
        {
            this.shutdownToken = new CancellationToken();
            this.random = new Random();

            // create the key pair that block rewards will be sent to
            var keyPair = TransactionManager.CreateKeyPair();
            this._coinbasePrivateKey = keyPair.Item1;
            this._coinbasePublicKey = keyPair.Item2;

            // initialize unit test storage
            this._storageContext = new MemoryStorageContext();
            this._cacheContext = new CacheContext(this._storageContext);

            // initialize unit test rules
            this._rules = new UnitTestRules(this._cacheContext);

            // initialize blockchain calculator
            this._calculator = new BlockchainCalculator(this._rules, this._cacheContext, this.shutdownToken);

            // create and mine the genesis block
            this._genesisBlock = genesisBlock ?? MineEmptyBlock(0);

            // update genesis blockchain and add to storage
            this._rules.SetGenesisBlock(this._genesisBlock);
            this._currentBlockchain = this._rules.GenesisBlockchain;
            this._genesisChainedBlock = AddBlock(this._genesisBlock, null).Item2;
        }

        public UnitTestRules Rules { get { return this._rules; } }

        public BlockchainCalculator Calculator { get { return this._calculator; } }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public MemoryStorageContext StorageContext { get { return this._storageContext; } }

        public Block GenesisBlock { get { return this._genesisBlock; } }

        public ChainedBlock GenesisChainedBlock { get { return this._genesisChainedBlock; } }

        public Data.Blockchain CurrentBlockchain { get { return this._currentBlockchain; } }

        public ECPrivateKeyParameters CoinbasePrivateKey { get { return this._coinbasePrivateKey; } }

        public ECPublicKeyParameters CoinbasePublicKey { get { return this._coinbasePublicKey; } }

        public Block CreateEmptyBlock(UInt256 previousBlockHash)
        {
            var coinbaseTx = new Transaction
            (
                version: 0,
                inputs: ImmutableArray.Create
                (
                    new TxInput
                    (
                        previousTxOutputKey: new TxOutputKey
                        (
                            txHash: 0,
                            txOutputIndex: 0
                        ),
                        scriptSignature: ImmutableArray.Create(random.NextBytes(100)),
                        sequence: 0
                    )
                ),
                outputs: ImmutableArray.Create
                (
                    new TxOutput
                    (
                        value: 50 * SATOSHI_PER_BTC,
                        scriptPublicKey: ImmutableArray.Create(TransactionManager.CreatePublicKeyScript(_coinbasePublicKey))
                    )
                ),
                lockTime: 0
            );

            //Debug.WriteLine("Coinbase Tx Created: {0}".Format2(coinbaseTx.Hash.ToHexNumberString()));

            var transactions = ImmutableArray.Create(coinbaseTx);
            var merkleRoot = transactions.CalculateMerkleRoot();

            var block = new Block
            (
                header: new BlockHeader
                (
                    version: 0,
                    previousBlock: previousBlockHash,
                    merkleRoot: merkleRoot,
                    time: 0,
                    bits: DataCalculator.TargetToBits(this._rules.HighestTarget),
                    nonce: 0
                ),
                transactions: transactions
            );

            return block;
        }

        public Block CreateEmptyBlock(ChainedBlock prevChainedBlock)
        {
            return CreateEmptyBlock(prevChainedBlock.BlockHash);
        }

        public Block MineBlock(Block block)
        {
            var minedHeader = Miner.MineBlockHeader(block.Header, this._rules.HighestTarget);
            if (minedHeader == null)
                Assert.Fail("No block could be mined for test data header.");

            block = block.With(Header: minedHeader);

            return block;
        }

        public Block MineEmptyBlock(UInt256 previousBlockHash)
        {
            return MineBlock(CreateEmptyBlock(previousBlockHash));
        }

        public Block MineEmptyBlock(Block previousBlock)
        {
            return MineEmptyBlock(previousBlock.Hash);
        }

        public Tuple<Block, ChainedBlock> MineAndAddEmptyBlock(ChainedBlock prevChainedBlock)
        {
            var block = MineEmptyBlock(prevChainedBlock.BlockHash);

            return AddBlock(block, prevChainedBlock);
        }

        public Tuple<Block, ChainedBlock> MineAndAddBlock(Block block, ChainedBlock? prevChainedBlock)
        {
            var minedHeader = Miner.MineBlockHeader(block.Header, this._rules.HighestTarget);
            if (minedHeader == null)
                Assert.Fail("No block could be mined for test data header.");

            var minedBlock = block.With(Header: minedHeader);

            return AddBlock(minedBlock, prevChainedBlock);
        }

        public Tuple<Block, ChainedBlock> AddBlock(Block block, ChainedBlock? prevChainedBlock)
        {
            if (prevChainedBlock != null)
                Assert.AreEqual(block.Header.PreviousBlock, prevChainedBlock.Value.BlockHash);

            var chainedBlock = new ChainedBlock
            (
                block.Hash,
                block.Header.PreviousBlock,
                prevChainedBlock != null ? prevChainedBlock.Value.Height + 1 : 0,
                prevChainedBlock != null ? prevChainedBlock.Value.TotalWork + block.Header.CalculateWork() : block.Header.CalculateWork()
            );

            this.StorageContext.BlockStorage.TryWriteValue(
                new KeyValuePair<UInt256, WriteValue<Block>>(block.Hash, new WriteValue<Block>(block, IsCreate: true)));

            this.StorageContext.ChainedBlockStorage.TryWriteValue(
                new KeyValuePair<UInt256, WriteValue<ChainedBlock>>(block.Hash, new WriteValue<ChainedBlock>(chainedBlock, IsCreate: true)));

            ChooseNewWinner();

            return Tuple.Create(block, chainedBlock);
        }

        public long BlockCacheMemorySize
        {
            get { return long.MaxValue; }
        }

        public long HeaderCacheMemorySize
        {
            get { return long.MaxValue; }
        }

        public long ChainedBlockCacheMemorySize
        {
            get { return long.MaxValue; }
        }

        private void ChooseNewWinner()
        {
            //TODO if there is a valid blockchain with less work than invalid blockchains, it won't get picked up as this is currently implemented

            //TODO when there is a tie this method is not deterministic, causing TestSimpleBlockchainSplit to fail

            var leafChainedBlocks =
                this.StorageContext.ChainedBlockStorage.FindLeafChained()
                 .ToDictionary(x => x.BlockHash, x => x);

            while (leafChainedBlocks.Count > 0)
            {
                var newWinner = this._rules.SelectWinningChainedBlock(leafChainedBlocks.Values.ToList());
                leafChainedBlocks.Remove(newWinner.BlockHash);

                try
                {
                    // try to use the blockchain
                    using (var cancelToken = new CancellationTokenSource())
                    {
                        List<MissingDataException> missingData;
                        this._currentBlockchain = this._calculator.CalculateBlockchainFromExisting(this._currentBlockchain, newWinner, out missingData, cancelToken.Token);
                    }

                    // success, exit
                    return;
                }
                catch (ValidationException) { }

                // failure, try another candidate if present
            }
        }
    }
}
