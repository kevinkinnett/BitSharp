using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class TxOutputTest
    {
        [TestMethod]
        public void TestTxOutputEquality()
        {
            var randomTxOutput = RandomData.RandomTxOutput();

            var sameTxOutput = new TxOutput
            (
                value: randomTxOutput.Value,
                scriptPublicKey: ImmutableArray.Create(randomTxOutput.ScriptPublicKey.ToArray())
            );

            var differentTxOutputValue = new TxOutput
            (
                value: ~randomTxOutput.Value,
                scriptPublicKey: randomTxOutput.ScriptPublicKey
            );

            var differentTxOutputScriptPublicKey = new TxOutput
            (
                value: randomTxOutput.Value,
                scriptPublicKey: randomTxOutput.ScriptPublicKey.Add(0)
            );

            Assert.IsTrue(randomTxOutput.Equals(sameTxOutput));
            Assert.IsTrue(randomTxOutput == sameTxOutput);
            Assert.IsFalse(randomTxOutput != sameTxOutput);

            Assert.IsFalse(randomTxOutput.Equals(differentTxOutputValue));
            Assert.IsFalse(randomTxOutput == differentTxOutputValue);
            Assert.IsTrue(randomTxOutput != differentTxOutputValue);

            Assert.IsFalse(randomTxOutput.Equals(differentTxOutputScriptPublicKey));
            Assert.IsFalse(randomTxOutput == differentTxOutputScriptPublicKey);
            Assert.IsTrue(randomTxOutput != differentTxOutputScriptPublicKey);
        }
    }
}
