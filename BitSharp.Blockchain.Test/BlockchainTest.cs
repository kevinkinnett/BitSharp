using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Transactions;
using BitSharp.WireProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace BitSharp.Blockchain.Test
{
    [TestClass]
    public class BlockchainTest
    {
        private const UInt64 SATOSHI_PER_BTC = 100 * 1000 * 1000;

        private readonly Random random = new Random();

        [TestMethod]
        public void TestAddSingleBlock()
        {
            var blockchain = new MemoryBlockchain();

            var block = blockchain.MineAndAddEmptyBlock(blockchain.GenesisBlock);

            Assert.AreEqual(1, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block.Hash, blockchain.CurrentBlockchain.RootBlockHash);
        }

        [TestMethod]
        public void TestLongBlockchain()
        {
            var blockchain = new MemoryBlockchain();

            var count = 1.THOUSAND();

            var blockHash = blockchain.GenesisBlock.Hash;
            for (var i = 0; i < count; i++)
            {
                Debug.WriteLine("TestLongBlockchain mining block {0:#,##0}".Format2(i));
                blockHash = blockchain.MineAndAddEmptyBlock(blockHash).Hash;
            }

            Assert.AreEqual(count, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(blockHash, blockchain.CurrentBlockchain.RootBlockHash);
        }

        [TestMethod]
        public void TestSimpleSpend()
        {
            var blockchain = new MemoryBlockchain();

            // create a new keypair to spend to
            var toKeyPair = TransactionManager.CreateKeyPair();
            var toPrivateKey = toKeyPair.Item1;
            var toPublicKey = toKeyPair.Item2;

            // add some simple blocks
            var block1 = blockchain.MineAndAddEmptyBlock(blockchain.GenesisBlock);
            var block2 = blockchain.MineAndAddEmptyBlock(block1);

            // check
            Assert.AreEqual(2, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block2.Hash, blockchain.CurrentBlockchain.RootBlockHash);
            Assert.AreEqual(2, blockchain.CurrentBlockchain.Utxo.Count);

            // attempt to spend block 2's coinbase in block 3
            var block3 = blockchain.CreateEmptyBlock(block2);
            var spendTx = TransactionManager.CreateSpendTransaction(block2.Transactions[0], 0, (byte)ScriptHashType.SIGHASH_ALL, 50 * SATOSHI_PER_BTC, blockchain.CoinbasePrivateKey, blockchain.CoinbasePublicKey, toPublicKey);
            block3 = block3.WithAddedTransactions(spendTx);
            block3 = blockchain.MineAndAddBlock(block3);

            // check
            Assert.AreEqual(3, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block3.Hash, blockchain.CurrentBlockchain.RootBlockHash);
            Assert.AreEqual(3, blockchain.CurrentBlockchain.Utxo.Count);

            // add a simple block
            var block4 = blockchain.MineAndAddEmptyBlock(block3);

            // check
            Assert.AreEqual(4, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block4.Hash, blockchain.CurrentBlockchain.RootBlockHash);
            Assert.AreEqual(4, blockchain.CurrentBlockchain.Utxo.Count);
        }

        [TestMethod]
        public void TestDoubleSpend()
        {
            var blockchain = new MemoryBlockchain();

            // create a new keypair to spend to
            var toKeyPair = TransactionManager.CreateKeyPair();
            var toPrivateKey = toKeyPair.Item1;
            var toPublicKey = toKeyPair.Item2;

            // create a new keypair to double spend to
            var toKeyPairBad = TransactionManager.CreateKeyPair();
            var toPrivateKeyBad = toKeyPair.Item1;
            var toPublicKeyBad = toKeyPair.Item2;

            // add some simple blocks
            var block1 = blockchain.MineAndAddEmptyBlock(blockchain.GenesisBlock);
            var block2 = blockchain.MineAndAddEmptyBlock(block1);

            // check
            Assert.AreEqual(2, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block2.Hash, blockchain.CurrentBlockchain.RootBlockHash);
            Assert.AreEqual(2, blockchain.CurrentBlockchain.Utxo.Count);

            // spend block 2's coinbase in block 3
            var block3 = blockchain.CreateEmptyBlock(block2);
            var spendTx = TransactionManager.CreateSpendTransaction(block2.Transactions[0], 0, (byte)ScriptHashType.SIGHASH_ALL, 50 * SATOSHI_PER_BTC, blockchain.CoinbasePrivateKey, blockchain.CoinbasePublicKey, toPublicKey);
            block3 = block3.WithAddedTransactions(spendTx);
            block3 = blockchain.MineAndAddBlock(block3);

            // check
            Assert.AreEqual(3, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block3.Hash, blockchain.CurrentBlockchain.RootBlockHash);
            Assert.AreEqual(3, blockchain.CurrentBlockchain.Utxo.Count);

            // attempt to spend block 2's coinbase again in block 4
            var block4Bad = blockchain.CreateEmptyBlock(block3);
            var doubleSpendTx = TransactionManager.CreateSpendTransaction(block2.Transactions[0], 0, (byte)ScriptHashType.SIGHASH_ALL, 50 * SATOSHI_PER_BTC, blockchain.CoinbasePrivateKey, blockchain.CoinbasePublicKey, toPublicKeyBad);
            block4Bad = block4Bad.WithAddedTransactions(doubleSpendTx);
            block4Bad = blockchain.MineAndAddBlock(block4Bad);

            // check that bad block wasn't added
            Assert.AreEqual(3, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block3.Hash, blockchain.CurrentBlockchain.RootBlockHash);
            Assert.AreEqual(3, blockchain.CurrentBlockchain.Utxo.Count);

            // add a simple block
            var block4 = blockchain.MineAndAddEmptyBlock(block3);

            // check
            Assert.AreEqual(4, blockchain.CurrentBlockchain.Height);
            Assert.AreEqual(block4.Hash, blockchain.CurrentBlockchain.RootBlockHash);
            Assert.AreEqual(4, blockchain.CurrentBlockchain.Utxo.Count);
        }

        [TestMethod]
        public void TestSimpleBlockchainSplit()
        {
            // create the first blockchain
            var blockchain1 = new MemoryBlockchain();

            // add some simple blocks
            var block1 = blockchain1.MineAndAddEmptyBlock(blockchain1.GenesisBlock);
            var block2 = blockchain1.MineAndAddEmptyBlock(block1);

            // introduce a split
            var block3a = blockchain1.MineAndAddEmptyBlock(block2);
            var block3b = blockchain1.MineAndAddEmptyBlock(block2);

            // check that 3a is current as it was introduced first
            // TODO this must be based on difficulty in some way
            Assert.AreEqual(3, blockchain1.CurrentBlockchain.Height);
            Assert.AreEqual(block3a.Hash, blockchain1.CurrentBlockchain.RootBlockHash);
            //TODO Assert.AreEqual(3, blockchain1.CurrentBlockchain.Utxo.Count);

            // continue split
            var block4a = blockchain1.MineAndAddEmptyBlock(block3a);
            var block4b = blockchain1.MineAndAddEmptyBlock(block3b);

            // check
            Assert.AreEqual(4, blockchain1.CurrentBlockchain.Height);
            Assert.AreEqual(block4a.Hash, blockchain1.CurrentBlockchain.RootBlockHash);
            //TODO Assert.AreEqual(4, blockchain1.CurrentBlockchain.Utxo.Count);

            // resolve split, with other chain winning
            var block5b = blockchain1.MineAndAddEmptyBlock(block4b);

            // check that blockchain reorged to the winning chain
            Assert.AreEqual(5, blockchain1.CurrentBlockchain.Height);
            Assert.AreEqual(block5b.Hash, blockchain1.CurrentBlockchain.RootBlockHash);
            //TODO Assert.AreEqual(5, blockchain1.CurrentBlockchain.Utxo.Count);

            // continue on winning fork
            var block6b = blockchain1.MineAndAddEmptyBlock(block5b);

            // check that blockchain reorged to the winning chain
            Assert.AreEqual(6, blockchain1.CurrentBlockchain.Height);
            Assert.AreEqual(block6b.Hash, blockchain1.CurrentBlockchain.RootBlockHash);
            //TODO Assert.AreEqual(5, blockchain1.CurrentBlockchain.Utxo.Count);

            // create a second blockchain, reusing the genesis from the first
            var blockchain2 = new MemoryBlockchain(blockchain1.GenesisBlock);

            // add only the winning blocks to the second blockchain
            blockchain2.AddBlock(block1);
            blockchain2.AddBlock(block2);
            blockchain2.AddBlock(block3b);
            blockchain2.AddBlock(block4b);
            blockchain2.AddBlock(block5b);
            blockchain2.AddBlock(block6b);

            // check second blockchain
            Assert.AreEqual(6, blockchain2.CurrentBlockchain.Height);
            Assert.AreEqual(block6b.Hash, blockchain2.CurrentBlockchain.RootBlockHash);
            //TODO Assert.AreEqual(5, blockchain2.CurrentBlockchain.Utxo.Count);

            // verify that re-organized blockchain matches winning-only blockchain
            var actualUtxo = blockchain1.CurrentBlockchain.Utxo;
            var expectedUtxo = blockchain2.CurrentBlockchain.Utxo;

            Assert.IsTrue(expectedUtxo.SequenceEqual(actualUtxo));
        }
    }
}
