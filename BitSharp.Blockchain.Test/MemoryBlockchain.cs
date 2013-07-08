using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Transactions;
using BitSharp.WireProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    public class MemoryBlockchain : IBlockchainRetriever
    {
        private const UInt64 SATOSHI_PER_BTC = 100 * 1000 * 1000;

        private readonly CancellationToken shutdownToken;
        private readonly Random random;

        private readonly MemoryBlockDataStorage memoryBlockDataStorage;
        private readonly MemoryBlockHeaderStorage memoryBlockHeaderStorage;
        private readonly MemoryBlockMetadataStorage memoryBlockMetadataStorage;
        private readonly MemoryTransactionStorage memoryTransactionStorage;

        private readonly Block _genesisBlock;

        private readonly UnitTestRules rules;
        private readonly BlockchainCalculator calculator;

        private Blockchain _currentBlockchain;

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

            // initialize unit test rules and blockchain calculator
            this.rules = new UnitTestRules();
            this.calculator = new BlockchainCalculator(this.rules, this, this.shutdownToken);

            // initialize unit test storage
            this.memoryBlockDataStorage = new MemoryBlockDataStorage();
            this.memoryBlockHeaderStorage = new MemoryBlockHeaderStorage(this.memoryBlockDataStorage);
            this.memoryBlockMetadataStorage = new MemoryBlockMetadataStorage(this.memoryBlockDataStorage, this.rules);
            this.memoryTransactionStorage = new MemoryTransactionStorage();

            // create and mine the genesis block
            this._genesisBlock = genesisBlock ?? MineEmptyBlock(0);

            // update genesis blockchain and add to storage
            this.rules.SetGenesisBlock(this._genesisBlock);
            this._currentBlockchain = this.rules.GenesisBlockchain;
            AddBlock(this._genesisBlock);
        }

        public Block GenesisBlock { get { return this._genesisBlock; } }

        public Blockchain CurrentBlockchain { get { return this._currentBlockchain; } }

        public ECPrivateKeyParameters CoinbasePrivateKey { get { return this._coinbasePrivateKey; } }

        public ECPublicKeyParameters CoinbasePublicKey { get { return this._coinbasePublicKey; } }

        public Block CreateEmptyBlock(UInt256 previousBlockHash)
        {
            var coinbaseTx = new Transaction
            (
                Version: 0,
                Inputs: ImmutableArray.Create
                (
                    new TransactionIn
                    (
                        PreviousTransactionHash: 0,
                        PreviousTransactionIndex: 0,
                        ScriptSignature: ImmutableArray.Create(random.NextBytes(100)),
                        Sequence: 0
                    )
                ),
                Outputs: ImmutableArray.Create
                (
                    new TransactionOut
                    (
                        Value: 50 * SATOSHI_PER_BTC,
                        ScriptPublicKey: ImmutableArray.Create(TransactionManager.CreatePublicKeyScript(_coinbasePublicKey))
                    )
                ),
                LockTime: 0
            );

            //Debug.WriteLine("Coinbase Tx Created: {0}".Format2(coinbaseTx.Hash.ToHexNumberString()));

            var transactions = ImmutableArray.Create(coinbaseTx);
            var merkleRoot = transactions.CalculateMerkleRoot();

            var block = new Block
            (
                Header: new BlockHeader
                (
                    Version: 0,
                    PreviousBlock: previousBlockHash,
                    MerkleRoot: merkleRoot,
                    Time: 0,
                    Bits: this.rules.HighestTargetBits,
                    Nonce: 0
                ),
                Transactions: transactions
            );

            return block;
        }

        public Block CreateEmptyBlock(Block previousBlock)
        {
            return CreateEmptyBlock(previousBlock.Hash);
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

        public Block MineAndAddEmptyBlock(UInt256 previousBlockHash)
        {
            var block = MineEmptyBlock(previousBlockHash);

            AddBlock(block);

            return block;
        }

        public Block MineAndAddEmptyBlock(Block previousBlock)
        {
            return MineAndAddEmptyBlock(previousBlock.Hash);
        }

        public Block MineAndAddBlock(Block block)
        {
            var minedHeader = Miner.MineBlockHeader(block.Header, this.rules.HighestTarget);
            if (minedHeader == null)
                Assert.Fail("No block could be mined for test data header.");

            var minedBlock = block.With(Header: minedHeader);

            AddBlock(minedBlock);

            return minedBlock;
        }

        public void AddBlock(Block block)
        {
            this.memoryBlockDataStorage.TryWriteValues(
                new KeyValuePair<UInt256, WriteValue<Block>>[]{
                    new KeyValuePair<UInt256, WriteValue<Block>>(block.Hash, new WriteValue<Block>( block, IsCreate:true))
                });

            ChooseNewWinner();
        }

        public bool TryGetBlock(UInt256 blockHash, out Block block, bool saveInCache = true)
        {
            return this.memoryBlockDataStorage.TryReadValue(blockHash, out block);
        }

        public bool TryGetBlockHeader(UInt256 blockHash, out BlockHeader blockHeader, bool saveInCache = true)
        {
            return this.memoryBlockHeaderStorage.TryReadValue(blockHash, out blockHeader);
        }

        public bool TryGetBlockMetadata(UInt256 blockHash, out BlockMetadata blockMetadata, bool saveInCache = true)
        {
            return this.memoryBlockMetadataStorage.TryReadValue(blockHash, out blockMetadata);
        }

        public bool TryGetTransaction(UInt256 transactionHash, out Transaction transaction, bool saveInCache = true)
        {
            return this.memoryTransactionStorage.TryReadValue(transactionHash, out transaction);
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

            var candidates =
                this.memoryBlockMetadataStorage.FindWinningChainedBlocks(new Dictionary<UInt256, BlockMetadata>())
                .ToDictionary(x => x.BlockHash, x => x);

            while (candidates.Count > 0)
            {
                var newWinner = this.rules.SelectWinningBlockchain(candidates.Values);
                candidates.Remove(newWinner.BlockHash);

                try
                {
                    // try to use the blockchain
                    this._currentBlockchain = this.calculator.CalculateBlockchainFromExisting(this._currentBlockchain, newWinner);
                    
                    // success, exit
                    return;
                }
                catch (ValidationException) { }
                
                // failure, try another candidate if present
            }
        }
    }
}
