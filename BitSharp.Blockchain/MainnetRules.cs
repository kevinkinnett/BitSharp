using BitSharp.Blockchain.ExtensionMethods;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Script;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class MainnetRules : IBlockchainRules
    {
        public static bool BypassValidation { get; set; }

        public static bool BypassExecuteScript { get; set; }

        private const UInt64 SATOSHI_PER_BTC = 100 * 1000 * 1000;

        private readonly CacheContext _cacheContext;
        private readonly UInt256 _highestTarget;
        private readonly UInt32 _highestTargetBits;
        private readonly Block _genesisBlock;
        private readonly BlockMetadata _genesisBlockMetadata;
        private readonly Data.Blockchain _genesisBlockchain;
        private readonly int _difficultyInternal = 2016;
        private readonly long _difficultyTargetTimespan = 14 * 24 * 60 * 60;

        public MainnetRules(CacheContext cacheContext)
        {
            this._cacheContext = cacheContext;

            this._highestTarget = UInt256.Parse("00000000FFFF0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            this._highestTargetBits = 0x1d00ffff;

            this._genesisBlock =
                new Block
                (
                    header: new BlockHeader
                    (
                        version: 1,
                        previousBlock: 0,
                        merkleRoot: UInt256.Parse("4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b", NumberStyles.HexNumber),
                        time: 1231006505,
                        bits: 486604799,
                        nonce: 2083236893
                    ),
                    transactions: ImmutableArray.Create
                    (
                        new Transaction
                        (
                            version: 1,
                            inputs: ImmutableArray.Create
                            (
                                new TxInput
                                (
                                    previousTxOutputKey: new TxOutputKey
                                    (
                                        txHash: 0,
                                        txOutputIndex: 0xFFFFFFFF
                                    ),
                                    scriptSignature: ImmutableArray.Create<byte>
                                    (
                                        0x04, 0xFF, 0xFF, 0x00, 0x1D, 0x01, 0x04, 0x45, 0x54, 0x68, 0x65, 0x20, 0x54, 0x69, 0x6D, 0x65,
                                        0x73, 0x20, 0x30, 0x33, 0x2F, 0x4A, 0x61, 0x6E, 0x2F, 0x32, 0x30, 0x30, 0x39, 0x20, 0x43, 0x68,
                                        0x61, 0x6E, 0x63, 0x65, 0x6C, 0x6C, 0x6F, 0x72, 0x20, 0x6F, 0x6E, 0x20, 0x62, 0x72, 0x69, 0x6E,
                                        0x6B, 0x20, 0x6F, 0x66, 0x20, 0x73, 0x65, 0x63, 0x6F, 0x6E, 0x64, 0x20, 0x62, 0x61, 0x69, 0x6C,
                                        0x6F, 0x75, 0x74, 0x20, 0x66, 0x6F, 0x72, 0x20, 0x62, 0x61, 0x6E, 0x6B, 0x73
                                    ),
                                    sequence: 0xFFFFFFFF
                                )
                            ),
                            outputs: ImmutableArray.Create
                            (
                                new TxOutput
                                (
                                    value: 50 * SATOSHI_PER_BTC,
                                    scriptPublicKey: ImmutableArray.Create<byte>
                                    (
                                        0x41, 0x04, 0x67, 0x8A, 0xFD, 0xB0, 0xFE, 0x55, 0x48, 0x27, 0x19, 0x67, 0xF1, 0xA6, 0x71, 0x30,
                                        0xB7, 0x10, 0x5C, 0xD6, 0xA8, 0x28, 0xE0, 0x39, 0x09, 0xA6, 0x79, 0x62, 0xE0, 0xEA, 0x1F, 0x61,
                                        0xDE, 0xB6, 0x49, 0xF6, 0xBC, 0x3F, 0x4C, 0xEF, 0x38, 0xC4, 0xF3, 0x55, 0x04, 0xE5, 0x1E, 0xC1,
                                        0x12, 0xDE, 0x5C, 0x38, 0x4D, 0xF7, 0xBA, 0x0B, 0x8D, 0x57, 0x8A, 0x4C, 0x70, 0x2B, 0x6B, 0xF1,
                                        0x1D, 0x5F, 0xAC
                                    )
                                )
                            ),
                            lockTime: 0
                        )
                    )
                );

            this._genesisBlockMetadata =
                new BlockMetadata
                (
                    blockHash: this._genesisBlock.Hash,
                    previousBlockHash: this._genesisBlock.Header.PreviousBlock,
                    work: this._genesisBlock.Header.CalculateWork(),
                    height: 0,
                    totalWork: this._genesisBlock.Header.CalculateWork(),
                    isValid: true
                );

            this._genesisBlockchain =
                new Data.Blockchain
                (
                    blockList: ImmutableList.Create(this._genesisBlockMetadata),
                    blockListHashes: ImmutableHashSet.Create(this._genesisBlock.Hash),
                    utxo: ImmutableHashSet.Create<TxOutputKey>() // genesis block coinbase is not included in utxo, it is unspendable
                );
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public virtual UInt256 HighestTarget { get { return this._highestTarget; } }

        public virtual UInt32 HighestTargetBits { get { return this._highestTargetBits; } }

        public virtual Block GenesisBlock { get { return this._genesisBlock; } }

        public virtual BlockMetadata GenesisBlockMetadata { get { return this._genesisBlockMetadata; } }

        public virtual Data.Blockchain GenesisBlockchain { get { return this._genesisBlockchain; } }

        public virtual int DifficultyInternal { get { return this._difficultyInternal; } }

        public virtual long DifficultyTargetTimespan { get { return this._difficultyTargetTimespan; } }

        public virtual double TargetToDifficulty(UInt256 target)
        {
            // implementation is equivalent of HighestTarget / target

            // perform the division
            UInt256 remainder;
            var result = UInt256.DivRem(HighestTarget, target, out remainder);

            // count the leading zeros in the highest target, use this to determine significant digits in the division
            var insignificant = HighestTarget.ToByteArray().Reverse().TakeWhile(x => x == 0).Count();

            // take only as many significant digits as can fit into a double (8 bytes) to calculate the fractional value
            var remainderDouble = (double)(Bits.ToUInt64((remainder >> (8 * insignificant)).ToByteArray().Take(8).ToArray()));
            var divisorDouble = (double)(Bits.ToUInt64((target >> (8 * insignificant)).ToByteArray().Take(8).ToArray()));

            // return the difficulty whole value plus the fractional value
            return (double)result + (remainderDouble / divisorDouble);
        }

        public virtual UInt256 DifficultyToTarget(double difficulty)
        {
            // implementation is equivalent of HighestTarget / difficulty

            // multiply difficulty and HighestTarget by a scale so that the decimal portion can be fed into a BigInteger
            var scale = 0x100000000L;
            var highestTargetScaled = (BigInteger)HighestTarget * scale;
            var difficultyScaled = (BigInteger)(difficulty * scale);

            // do the division
            var target = highestTargetScaled / difficultyScaled;

            // get the resulting target bytes, taking only the 3 most significant
            var targetBytes = target.ToByteArray();
            targetBytes = new byte[targetBytes.Length - 3].Concat(targetBytes.Skip(targetBytes.Length - 3).ToArray());

            // return the target
            return new UInt256(targetBytes);
        }

        public virtual UInt256 GetRequiredNextTarget(Data.Blockchain blockchain)
        {
            try
            {
                // lookup genesis block header
                var genesisBlockHeader = this.CacheContext.GetBlockHeader(blockchain.BlockList[0].BlockHash);

                // lookup the latest block on the current blockchain
                var currentBlockHeader = this.CacheContext.GetBlockHeader(blockchain.RootBlockHash);

                // use genesis block difficulty if first adjusment interval has not yet been reached
                if (blockchain.Height < DifficultyInternal)
                {
                    return genesisBlockHeader.CalculateTarget();
                }
                // not on an adjustment interval, reuse current block's target
                else if (blockchain.Height % DifficultyInternal != 0)
                {
                    return currentBlockHeader.CalculateTarget();
                }
                // on an adjustment interval, calculate the required next target
                else
                {
                    // get the block difficultyInterval blocks ago
                    var startBlockMetadata = blockchain.BlockList.Reverse().Skip(DifficultyInternal).First();
                    var startBlockHeader = this.CacheContext.GetBlockHeader(startBlockMetadata.BlockHash);
                    Debug.Assert(startBlockMetadata.Height == blockchain.Height - DifficultyInternal);

                    var actualTimespan = (long)currentBlockHeader.Time - (long)startBlockHeader.Time;
                    var targetTimespan = DifficultyTargetTimespan;

                    // limit adjustment to 4x or 1/4x
                    if (actualTimespan < targetTimespan / 4)
                        actualTimespan = targetTimespan / 4;
                    else if (actualTimespan > targetTimespan * 4)
                        actualTimespan = targetTimespan * 4;

                    // calculate the new target
                    var target = startBlockHeader.CalculateTarget();
                    target *= actualTimespan;
                    target /= targetTimespan;

                    // make sure target isn't too high (too low difficulty)
                    if (target > HighestTarget)
                        target = HighestTarget;

                    return target;
                }
            }
            catch (ArgumentException)
            {
                // invalid bits
                Debugger.Break();
                throw new ValidationException();
            }
        }

        public virtual void ValidateBlock(Block block, Data.Blockchain blockchain, ImmutableDictionary<UInt256, Transaction> transactions)
        {
            //TODO
            if (BypassValidation)
                return;

            // calculate the next required target
            var requiredTarget = GetRequiredNextTarget(blockchain);

            // validate block's target against the required target
            var blockTarget = block.Header.CalculateTarget();
            if (blockTarget > requiredTarget)
            {
                throw new ValidationException("Failing block {0} at height {1}: Block target {2} did not match required target of {3}".Format2(block.Hash.ToHexNumberString(), blockchain.Height, blockTarget.ToHexNumberString(), requiredTarget.ToHexNumberString()));
            }

            // validate block's proof of work against its stated target
            if (block.Hash > blockTarget || block.Hash > requiredTarget)
            {
                throw new ValidationException("Failing block {0} at height {1}: Block did not match its own target of {2}".Format2(block.Hash.ToHexNumberString(), blockchain.Height, blockTarget.ToHexNumberString()));
            }

            // ensure there is at least 1 transaction
            if (block.Transactions.Length == 0)
            {
                throw new ValidationException("Failing block {0} at height {1}: Zero transactions present".Format2(block.Hash.ToHexNumberString(), blockchain.Height));
            }

            //TODO apply real coinbase rule
            // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
            var coinbaseTx = block.Transactions[0];

            // check that coinbase has only one input
            if (coinbaseTx.Inputs.Length != 1)
            {
                throw new ValidationException("Failing block {0} at height {1}: Coinbase transaction does not have exactly one input".Format2(block.Hash.ToHexNumberString(), blockchain.Height));
            }

            // validate transactions in parallel
            long unspentValue = 0L;
            try
            {
                Parallel.For(1, block.Transactions.Length, (txIndex, loopState) =>
                {
                    var tx = block.Transactions[txIndex];

                    long unspentValueInner;
                    ValidateTransaction(blockchain.Height, tx, txIndex, out unspentValueInner, transactions);

                    Interlocked.Add(ref unspentValue, unspentValueInner);
                });
            }
            catch (AggregateException e)
            {
                var validationException = e.InnerExceptions.FirstOrDefault(x => x is ValidationException);
                var missingDataException = e.InnerExceptions.FirstOrDefault(x => x is MissingDataException);

                if (validationException != null)
                    throw validationException;

                if (missingDataException != null)
                    throw missingDataException;

                throw;
            }

            // calculate the expected reward in coinbase
            var expectedReward = (long)(50 * SATOSHI_PER_BTC);
            if (blockchain.Height / 210000 <= 32)
                expectedReward /= (long)Math.Pow(2, blockchain.Height / 210000);
            expectedReward += unspentValue;

            // calculate the actual reward in coinbase
            var actualReward = 0L;
            foreach (var txOutput in coinbaseTx.Outputs)
                actualReward += (long)txOutput.Value;

            // ensure coinbase has correct reward
            if (actualReward > expectedReward)
            {
                throw new ValidationException("Failing block {0} at height {1}: Coinbase value is greater than reward + fees".Format2(block.Hash.ToHexNumberString(), blockchain.Height));
            }

            // all validation has passed
        }

        public virtual void ValidateTransaction(long blockHeight, Transaction tx, int txIndex, out long unspentValue, ImmutableDictionary<UInt256, Transaction> transactions)
        {
            unspentValue = -1;

            // lookup all previous outputs
            var prevOutputMissing = false;
            var previousOutputs = new Dictionary<TxOutputKey, Tuple<TxInput, int, TxOutput>>();
            for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
            {
                var input = tx.Inputs[inputIndex];

                // find previous transaction
                if (!transactions.ContainsKey(input.PreviousTxOutputKey.TxHash))
                    throw new MissingDataException(DataType.Transaction, input.PreviousTxOutputKey.TxHash);
                var prevTx = transactions[input.PreviousTxOutputKey.TxHash];

                // find previous transaction output
                if (input.PreviousTxOutputKey.TxOutputIndex >= prevTx.Outputs.Length)
                    throw new ValidationException();
                var prevOutput = prevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];

                previousOutputs.Add(input.PreviousTxOutputKey, Tuple.Create(input, inputIndex, prevOutput));
            }

            if (prevOutputMissing)
            {
                throw new ValidationException();
            }

            // verify spend amounts
            var txInputValue = (UInt64)0;
            var txOutputValue = (UInt64)0;

            for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
            {
                var input = tx.Inputs[inputIndex];

                // add transactions previous value to unspent amount (used to calculate allowed coinbase reward)
                var prevOutput = previousOutputs[input.PreviousTxOutputKey].Item3;
                txInputValue += prevOutput.Value;
            }

            for (var outputIndex = 0; outputIndex < tx.Outputs.Length; outputIndex++)
            {
                // remove transactions spend value from unspent amount (used to calculate allowed coinbase reward)
                var output = tx.Outputs[outputIndex];
                txOutputValue += output.Value;
            }

            // ensure that amount being output from transaction isn't greater than amount being input
            if (txOutputValue > txInputValue)
            {
                throw new ValidationException("Failing tx {0}: Transaction output value is greater than input value".Format2(tx.Hash.ToHexNumberString()));
            }

            // verify scripts
            var scriptFailed = false;
            if (!BypassExecuteScript)
            {
                var scriptEngine = new ScriptEngine();

                //Parallel.ForEach(previousOutputs.Values, tuple =>
                foreach (var tuple in previousOutputs.Values)
                {
                    var input = tuple.Item1;
                    var inputIndex = tuple.Item2;
                    var prevOutput = tuple.Item3;

                    // create the transaction script from the input and output
                    var script = input.ScriptSignature.AddRange(prevOutput.ScriptPublicKey);
                    try
                    {
                        if (!scriptEngine.VerifyScript(0 /*TODO blockHash*/, txIndex, prevOutput.ScriptPublicKey.ToArray(), tx, inputIndex, script.ToArray()))
                        {
                            //TDOO fail loop immediately
                            scriptFailed = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Tx {0} threw exception: {1}\n{2}".Format2(tx.Hash.ToHexNumberString(), e.Message, e.ToString()));
                        //TDOO fail loop immediately
                        scriptFailed = true;
                    }
                }
                //});
            }

            // check if any scripts failed
            if (scriptFailed)
            {
                throw new ValidationException("Failing tx {0}: Transaction script failed verification".Format2(tx.Hash.ToHexNumberString()));
            }

            // calculate unspent value
            unspentValue = (long)(txInputValue - txOutputValue);

            // sanity check
            if (unspentValue < 0)
            {
                throw new ValidationException();
            }

            // all validation has passed
        }

        public BlockMetadata SelectWinningBlockchain(IEnumerable<BlockMetadata> candidateBlockchains)
        {
            var maxTotalWork = candidateBlockchains.Max(x => x.TotalWork);
            return candidateBlockchains.First(x => x.TotalWork == maxTotalWork);
        }
    }
}
