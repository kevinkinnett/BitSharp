using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using BitSharp.Storage.Test;
using BitSharp.Transactions;
using BitSharp.WireProtocol;
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
        private readonly BlockMetadata _genesisBlockMetadata;

        private readonly UnitTestRules rules;
        private readonly BlockchainCalculator calculator;

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
            this.rules = new UnitTestRules(this._cacheContext);

            // initialize blockchain calculator
            this.calculator = new BlockchainCalculator(this.rules, this._cacheContext, this.shutdownToken);

            // create and mine the genesis block
            this._genesisBlock = genesisBlock ?? MineEmptyBlock(0);

            // update genesis blockchain and add to storage
            this.rules.SetGenesisBlock(this._genesisBlock);
            this._currentBlockchain = this.rules.GenesisBlockchain;
            this._genesisBlockMetadata = AddBlock(this._genesisBlock, null).Item2;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public MemoryStorageContext StorageContext { get { return this._storageContext; } }

        public Block GenesisBlock { get { return this._genesisBlock; } }

        public BlockMetadata GenesisBlockMetadata { get { return this._genesisBlockMetadata; } }

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
                    bits: this.rules.HighestTargetBits,
                    nonce: 0
                ),
                transactions: transactions
            );

            return block;
        }

        public Block CreateEmptyBlock(BlockMetadata previousBlockMetadata)
        {
            return CreateEmptyBlock(previousBlockMetadata.BlockHash);
        }

        public Block MineBlock(Block block)
        {
            var minedHeader = Miner.MineBlockHeader(block.Header, this.rules.HighestTarget);
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

        public Tuple<Block, BlockMetadata> MineAndAddEmptyBlock(BlockMetadata previousBlockMetadata)
        {
            var block = MineEmptyBlock(previousBlockMetadata.BlockHash);

            return AddBlock(block, previousBlockMetadata);
        }

        public Tuple<Block, BlockMetadata> MineAndAddBlock(Block block, BlockMetadata? previousBlockMetadata)
        {
            var minedHeader = Miner.MineBlockHeader(block.Header, this.rules.HighestTarget);
            if (minedHeader == null)
                Assert.Fail("No block could be mined for test data header.");

            var minedBlock = block.With(Header: minedHeader);

            return AddBlock(minedBlock, previousBlockMetadata);
        }

        public Tuple<Block, BlockMetadata> AddBlock(Block block, BlockMetadata? previousBlockMetadata)
        {
            if (previousBlockMetadata != null)
                Assert.AreEqual(block.Header.PreviousBlock, previousBlockMetadata.Value.BlockHash);

            var blockMetadata = new BlockMetadata
            (
                block.Hash,
                block.Header.PreviousBlock,
                block.Header.CalculateWork(),
                previousBlockMetadata != null ? previousBlockMetadata.Value.Height.Value + 1 : 0,
                previousBlockMetadata != null ? previousBlockMetadata.Value.TotalWork.Value + block.Header.CalculateWork() : block.Header.CalculateWork(),
                isValid: null
            );

            this.StorageContext.BlockStorage.TryWriteValue(
                new KeyValuePair<UInt256, WriteValue<Block>>(block.Hash, new WriteValue<Block>(block, IsCreate: true)));

            this.StorageContext.BlockMetadataStorage.TryWriteValue(
                new KeyValuePair<UInt256, WriteValue<BlockMetadata>>(block.Hash, new WriteValue<BlockMetadata>(blockMetadata, IsCreate: true)));

            ChooseNewWinner();

            return Tuple.Create(block, blockMetadata);
        }

        public long BlockCacheMemorySize
        {
            get { return long.MaxValue; }
        }

        public long HeaderCacheMemorySize
        {
            get { return long.MaxValue; }
        }

        public long MetadataCacheMemorySize
        {
            get { return long.MaxValue; }
        }

        private void ChooseNewWinner()
        {
            //TODO if there is a valid blockchain with less work than invalid blockchains, it won't get picked up as this is currently implemented

            //TODO when there is a tie this method is not deterministic, causing TestSimpleBlockchainSplit to fail

            var candidates =
                this.StorageContext.BlockMetadataStorage.FindWinningChainedBlocks(new Dictionary<UInt256, BlockMetadata>())
                .ToDictionary(x => x.BlockHash, x => x);

            while (candidates.Count > 0)
            {
                var newWinner = this.rules.SelectWinningBlockchain(candidates.Values);
                candidates.Remove(newWinner.BlockHash);

                try
                {
                    // try to use the blockchain
                    using (var cancelToken = new CancellationTokenSource())
                    {
                        List<MissingDataException> missingData;
                        this._currentBlockchain = this.calculator.CalculateBlockchainFromExisting(this._currentBlockchain, newWinner, out missingData, cancelToken.Token);
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
