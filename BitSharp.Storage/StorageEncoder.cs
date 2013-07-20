using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class StorageEncoder
    {
        public static Block DecodeBlock(Stream stream, UInt256? blockHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new Block
                (
                    header: DecodeBlockHeader(stream, blockHash),
                    transactions: DecodeList(reader, () => DecodeTransaction(stream))
                );
            }
        }

        public static void EncodeBlock(Stream stream, Block block)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                EncodeBlockHeader(stream, block.Header);
                EncodeList(writer, block.Transactions, tx => EncodeTransaction(stream, tx));
            }
        }

        public static byte[] EncodeBlock(Block block)
        {
            var stream = new MemoryStream();
            EncodeBlock(stream, block);
            return stream.ToArray();
        }

        public static BlockHeader DecodeBlockHeader(Stream stream, UInt256? blockHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new BlockHeader
                (
                    version: reader.Read4Bytes(),
                    previousBlock: reader.Read32Bytes(),
                    merkleRoot: reader.Read32Bytes(),
                    time: reader.Read4Bytes(),
                    bits: reader.Read4Bytes(),
                    nonce: reader.Read4Bytes(),
                    hash: blockHash
                );
            }
        }

        public static void EncodeBlockHeader(Stream stream, BlockHeader blockHeader)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(blockHeader.Version);
                writer.Write32Bytes(blockHeader.PreviousBlock);
                writer.Write32Bytes(blockHeader.MerkleRoot);
                writer.Write4Bytes(blockHeader.Time);
                writer.Write4Bytes(blockHeader.Bits);
                writer.Write4Bytes(blockHeader.Nonce);
            }
        }

        public static byte[] EncodeBlockHeader(BlockHeader blockHeader)
        {
            var stream = new MemoryStream();
            EncodeBlockHeader(stream, blockHeader);
            return stream.ToArray();
        }

        public static Transaction DecodeTransaction(Stream stream, UInt256? txHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new Transaction
                (
                    version: reader.Read4Bytes(),
                    inputs: DecodeList(reader, () => DecodeTxInput(stream)),
                    outputs: DecodeList(reader, () => DecodeTxOutput(stream)),
                    lockTime: reader.Read4Bytes(),
                    hash: txHash
                );
            }
        }

        public static void EncodeTransaction(Stream stream, Transaction tx)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(tx.Version);
                EncodeList(writer, tx.Inputs, input => EncodeTxInput(stream, input));
                EncodeList(writer, tx.Outputs, output => EncodeTxOutput(stream, output));
                writer.Write4Bytes(tx.LockTime);
            }
        }

        public static byte[] EncodeTransaction(Transaction tx)
        {
            var stream = new MemoryStream();
            EncodeTransaction(stream, tx);
            return stream.ToArray();
        }

        public static TxInput DecodeTxInput(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new TxInput
                (
                    previousTxOutputKey: new TxOutputKey
                    (
                        txHash: reader.Read32Bytes(),
                        txOutputIndex: reader.Read4Bytes()
                    ),
                    scriptSignature: DecodeVarBytes(reader).ToImmutableArray(),
                    sequence: reader.Read4Bytes()
                );
            }
        }

        public static void EncodeTxInput(Stream stream, TxInput txInput)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(txInput.PreviousTxOutputKey.TxHash);
                writer.Write4Bytes(txInput.PreviousTxOutputKey.TxOutputIndex);
                EncodeVarBytes(writer, txInput.ScriptSignature.ToArray());
                writer.Write4Bytes(txInput.Sequence);
            }
        }

        public static byte[] EncodeTxInput(TxInput txInput)
        {
            var stream = new MemoryStream();
            EncodeTxInput(stream, txInput);
            return stream.ToArray();
        }

        public static TxOutput DecodeTxOutput(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new TxOutput
                (
                    value: reader.Read8Bytes(),
                    scriptPublicKey: DecodeVarBytes(reader).ToImmutableArray()
                );
            }
        }

        public static void EncodeTxOutput(Stream stream, TxOutput txOutput)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write8Bytes(txOutput.Value);
                EncodeVarBytes(writer, txOutput.ScriptPublicKey.ToArray());
            }
        }

        public static byte[] EncodeTxOutput(TxOutput txOutput)
        {
            var stream = new MemoryStream();
            EncodeTxOutput(stream, txOutput);
            return stream.ToArray();
        }

        public static byte[] DecodeVarBytes(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            return reader.ReadBytes(length);
        }

        public static void EncodeVarBytes(BinaryWriter writer, byte[] bytes)
        {
            writer.WriteInt32(bytes.Length);
            writer.WriteBytes(bytes);
        }

        public static ImmutableArray<T> DecodeList<T>(BinaryReader reader, Func<T> decode)
        {
            var length = reader.ReadInt32();

            var list = new T[length];
            for (var i = 0; i < length; i++)
            {
                list[i] = decode();
            }

            return list.ToImmutableArray();
        }

        public static void EncodeList<T>(BinaryWriter writer, ImmutableArray<T> list, Action<T> encode)
        {
            writer.WriteInt32(list.Length);

            for (var i = 0; i < list.Length; i++)
            {
                encode(list[i]);
            }
        }
    }
}
