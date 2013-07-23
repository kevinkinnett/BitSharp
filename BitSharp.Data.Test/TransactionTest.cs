using BitSharp.Common.ExtensionMethods;
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
    public class TransactionTest
    {
        [TestMethod]
        public void TestTransactionIsDefault()
        {
            var defaultTransaction = default(Transaction);
            Assert.IsTrue(defaultTransaction.IsDefault);

            var randomTransaction = RandomData.RandomTransaction();
            Assert.IsFalse(randomTransaction.IsDefault);
        }

        [TestMethod]
        public void TestTransactionEquality()
        {
            var randomTransaction = RandomData.RandomTransaction();

            var sameTransaction = new Transaction
            (
                version: randomTransaction.Version,
                inputs: ImmutableArray.Create(randomTransaction.Inputs.ToArray()),
                outputs: ImmutableArray.Create(randomTransaction.Outputs.ToArray()),
                lockTime: randomTransaction.LockTime
            );

            var differentTransactionVersion = new Transaction
            (
                version: ~randomTransaction.Version,
                inputs: randomTransaction.Inputs,
                outputs: randomTransaction.Outputs,
                lockTime: randomTransaction.LockTime
            );

            var differentTransactionInputs = new Transaction
            (
                version: randomTransaction.Version,
                inputs: ImmutableArray.Create(randomTransaction.Inputs.Concat(randomTransaction.Inputs.Last()).ToArray()),
                outputs: randomTransaction.Outputs,
                lockTime: randomTransaction.LockTime
            );

            var differentTransactionOutputs = new Transaction
            (
                version: randomTransaction.Version,
                inputs: randomTransaction.Inputs,
                outputs: ImmutableArray.Create(randomTransaction.Outputs.Concat(randomTransaction.Outputs.Last()).ToArray()),
                lockTime: randomTransaction.LockTime
            );

            var differentTransactionLockTime = new Transaction
            (
                version: randomTransaction.Version,
                inputs: randomTransaction.Inputs,
                outputs: randomTransaction.Outputs,
                lockTime: ~randomTransaction.LockTime
            );

            Assert.IsTrue(randomTransaction.Equals(sameTransaction));
            Assert.IsTrue(randomTransaction == sameTransaction);
            Assert.IsFalse(randomTransaction != sameTransaction);

            Assert.IsFalse(randomTransaction.Equals(differentTransactionVersion));
            Assert.IsFalse(randomTransaction == differentTransactionVersion);
            Assert.IsTrue(randomTransaction != differentTransactionVersion);

            Assert.IsFalse(randomTransaction.Equals(differentTransactionInputs));
            Assert.IsFalse(randomTransaction == differentTransactionInputs);
            Assert.IsTrue(randomTransaction != differentTransactionInputs);

            Assert.IsFalse(randomTransaction.Equals(differentTransactionOutputs));
            Assert.IsFalse(randomTransaction == differentTransactionOutputs);
            Assert.IsTrue(randomTransaction != differentTransactionOutputs);

            Assert.IsFalse(randomTransaction.Equals(differentTransactionLockTime));
            Assert.IsFalse(randomTransaction == differentTransactionLockTime);
            Assert.IsTrue(randomTransaction != differentTransactionLockTime);
        }
    }
}
