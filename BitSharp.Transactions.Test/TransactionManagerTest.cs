using BitSharp.Script;
using BitSharp.Common.ExtensionMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Transactions;

namespace BitSharp.Transactions.Test
{
    [TestClass]
    public class TransactionManagerTest
    {
        [TestMethod]
        public void TestCreateCoinbaseAndSpend()
        {
            var keyPair = TransactionManager.CreateKeyPair();
            var privateKey = keyPair.Item1;
            var publicKey = keyPair.Item2;

            var coinbaseTx = TransactionManager.CreateCoinbaseTransaction(publicKey, Encoding.ASCII.GetBytes("coinbase text!"));

            var publicKeyScript = TransactionManager.CreatePublicKeyScript(publicKey);
            var privateKeyScript = TransactionManager.CreatePrivateKeyScript(coinbaseTx, 0, (byte)ScriptHashType.SIGHASH_ALL, privateKey, publicKey);

            var script = privateKeyScript.Concat(publicKeyScript);

            var scriptEngine = new ScriptEngine();
            Assert.IsTrue(scriptEngine.VerifyScript(0, 0, publicKeyScript.ToArray(), coinbaseTx, 0, script));
        }
    }
}
