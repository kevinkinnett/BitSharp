using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Script;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;

namespace BitSharp.BlockHelper
{
    public static class BlockJson
    {
        public static Block GetBlockFromJson(string blockJson)
        {
            var block = Json.Decode(blockJson);
            return new Block
            (
                header: new BlockHeader
                (
                    version: Convert.ToUInt32(block.ver),
                    previousBlock: UInt256.Parse(block.prev_block, NumberStyles.HexNumber),
                    merkleRoot: UInt256.Parse(block.mrkl_root, NumberStyles.HexNumber),
                    time: Convert.ToUInt32(block.time),
                    bits: Convert.ToUInt32(block.bits),
                    nonce: Convert.ToUInt32(block.nonce)
                ),
                transactions: ReadTransactions(block.tx)
            );
        }

        public static ImmutableArray<Transaction> ReadTransactions(dynamic transactions)
        {
            return
                Enumerable.Range(0, (int)transactions.Length)
                .Select(i => (Transaction)ReadTransaction(transactions[i]))
                .ToImmutableArray();
        }

        public static Transaction ReadTransaction(dynamic transaction)
        {
            return new Transaction
            (
                version: Convert.ToUInt32(transaction.ver),
                inputs: ReadInputs(transaction.@in),
                outputs: ReadOutputs(transaction.@out),
                lockTime: Convert.ToUInt32(transaction.lock_time)
            );
        }

        public static ImmutableArray<TxInput> ReadInputs(dynamic inputs)
        {
            return
                Enumerable.Range(0, (int)inputs.Length)
                .Select(i => (TxInput)ReadInput(inputs[i]))
                .ToImmutableArray();
        }

        public static ImmutableArray<TxOutput> ReadOutputs(dynamic outputs)
        {
            return
                Enumerable.Range(0, (int)outputs.Length)
                .Select(i => (TxOutput)ReadOutput(outputs[i]))
                .ToImmutableArray();
        }

        public static TxInput ReadInput(dynamic input)
        {
            return new TxInput
            (
                previousTxOutputKey: new TxOutputKey
                (
                    txHash: UInt256.Parse(input.prev_out.hash, NumberStyles.HexNumber),
                    txOutputIndex: Convert.ToUInt32(input.prev_out.n)
                ),
                scriptSignature: input.scriptSig != null ? ReadScript(input.scriptSig) : ReadCoinbase(input.coinbase),
                sequence: input.sequence != null ? Convert.ToUInt32(input.sequence) : 0xFFFFFFFF
            );
        }

        public static TxOutput ReadOutput(dynamic output)
        {
            return new TxOutput
            (
                value: Convert.ToUInt64(((string)output.value).Replace(".", "")), //TODO cleaner decimal replace
                scriptPublicKey: ReadScript(output.scriptPubKey)
            );
        }

        public static ImmutableArray<byte> ReadCoinbase(string data)
        {
            return data != null ? HexStringToByteArray(data) : ImmutableArray.Create<byte>();
        }

        public static ImmutableArray<byte> ReadScript(string data)
        {
            if (data == null)
                return ImmutableArray.Create<byte>();

            var bytes = new List<byte>();
            foreach (var x in data.Split(' '))
            {
                if (x.StartsWith("OP_"))
                {
                    bytes.Add((byte)(int)Enum.Parse(typeof(ScriptOp), x));
                }
                else
                {
                    var pushBytes = HexStringToByteArray(x);
                    if (pushBytes.Length >= (int)ScriptOp.OP_PUSHBYTES1 && pushBytes.Length <= (int)ScriptOp.OP_PUSHBYTES75)
                    {
                        bytes.Add((byte)pushBytes.Length);
                        bytes.AddRange(pushBytes);
                    }
                    else
                    {
                        throw new Exception("data is too long");
                    }
                }
            }

            return bytes.ToImmutableArray();
        }

        //TODO not actually an extension method...
        private static ImmutableArray<byte> HexStringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToImmutableArray();
        }
    }
}
