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
    public class TxInputTest
    {
        [TestMethod]
        public void TestTxInputEquality()
        {
            var randomTxInput = RandomData.RandomTxInput();

            var sameTxInput = new TxInput
            (
                previousTxOutputKey: new TxOutputKey
                (
                    txHash: randomTxInput.PreviousTxOutputKey.TxHash,
                    txOutputIndex: randomTxInput.PreviousTxOutputKey.TxOutputIndex
                ),
                scriptSignature: ImmutableArray.Create(randomTxInput.ScriptSignature.ToArray()),
                sequence: randomTxInput.Sequence
            );

            var differentTxInputPreviousTxOutputKey = new TxInput
            (
                previousTxOutputKey: new TxOutputKey
                (
                    txHash: ~randomTxInput.PreviousTxOutputKey.TxHash,
                    txOutputIndex: randomTxInput.PreviousTxOutputKey.TxOutputIndex
                ),
                scriptSignature: randomTxInput.ScriptSignature,
                sequence: randomTxInput.Sequence
            );

            var differentTxInputScriptSignature = new TxInput
            (
                previousTxOutputKey: randomTxInput.PreviousTxOutputKey,
                scriptSignature: randomTxInput.ScriptSignature.Add(0),
                sequence: randomTxInput.Sequence
            );

            var differentTxInputSequence = new TxInput
            (
                previousTxOutputKey: randomTxInput.PreviousTxOutputKey,
                scriptSignature: randomTxInput.ScriptSignature,
                sequence: ~randomTxInput.Sequence
            );

            Assert.IsTrue(randomTxInput.Equals(sameTxInput));
            Assert.IsTrue(randomTxInput == sameTxInput);
            Assert.IsFalse(randomTxInput != sameTxInput);

            Assert.IsFalse(randomTxInput.Equals(differentTxInputPreviousTxOutputKey));
            Assert.IsFalse(randomTxInput == differentTxInputPreviousTxOutputKey);
            Assert.IsTrue(randomTxInput != differentTxInputPreviousTxOutputKey);

            Assert.IsFalse(randomTxInput.Equals(differentTxInputScriptSignature));
            Assert.IsFalse(randomTxInput == differentTxInputScriptSignature);
            Assert.IsTrue(randomTxInput != differentTxInputScriptSignature);

            Assert.IsFalse(randomTxInput.Equals(differentTxInputSequence));
            Assert.IsFalse(randomTxInput == differentTxInputSequence);
            Assert.IsTrue(randomTxInput != differentTxInputSequence);
        }
    }
}
