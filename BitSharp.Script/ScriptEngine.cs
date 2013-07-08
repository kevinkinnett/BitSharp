using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math.EC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BitSharp.WireProtocol;
using BitSharp.WireProtocol.ExtensionMethods;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System.Collections.Concurrent;
using BigIntegerBouncy = Org.BouncyCastle.Math.BigInteger;
using System.Threading;
using System.Collections.Immutable;

namespace BitSharp.Script
{
    public class ScriptEngine
    {
        public static bool BypassVerifySignature { get; set; }

        private readonly X9ECParameters curve;
        private readonly ECDomainParameters domainParameters;

        public ScriptEngine()
        {
            this.curve = SecNamedCurves.GetByName("secp256k1");
            this.domainParameters = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());
        }

        public bool VerifyScript(UInt256 blockHash, int txIndex, byte[] scriptPubKey, Transaction tx, int inputIndex, byte[] script)
        {
//            logger.LogTrace(
//@"
//++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//Verifying script for block {0}, transaction {1}, input {2}
//{3}
//++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++"
//                , blockHash, txIndex, inputIndex, script.ToArray().ToHexDataString());

            Stack stack, altStack;
            if (
                ExecuteOps(scriptPubKey.ToImmutableArray(), tx, inputIndex, script, out stack, out altStack)
                && stack.Count == 1 && altStack.Count == 0)
            {
                var success = stack.PeekBool(); //TODO Pop? does it matter?

                // Additional validation for spend-to-script-hash transactions:
                //TODO

                return success;
            }
            else
            {
                return false;
            }
        }

        private bool ExecuteOps(ImmutableArray<byte> scriptPubKey, Transaction tx, int inputIndex, byte[] script, out Stack stack, out Stack altStack)
        {
            stack = new Stack();
            altStack = new Stack();

            var opReader = new WireReader(script.ToStream());

            while (opReader.Position < script.Length)
            {
                var opByte = opReader.Read1Byte();
                var op = (ScriptOp)Enum.ToObject(typeof(ScriptOp), opByte);

                //logger.LogTrace("Executing {0} with stack count: {1}", OpName(opByte), stack.Count);

                switch (op)
                {
                    // Constants
                    case ScriptOp.OP_PUSHDATA1:
                        {
                            if (opReader.Position + 1 >= script.Length)
                                return false;

                            var length = opReader.Read1Byte();
                            stack.PushBytes(opReader.ReadRawBytes(length));
                        }
                        break;

                    case ScriptOp.OP_PUSHDATA2:
                        {
                            if (opReader.Position + 2 >= script.Length)
                                return false;

                            var length = opReader.Read2Bytes();
                            stack.PushBytes(opReader.ReadRawBytes(length));
                        }
                        break;

                    case ScriptOp.OP_PUSHDATA4:
                        {
                            if (opReader.Position + 4 >= script.Length)
                                return false;

                            var length = opReader.Read4Bytes();
                            stack.PushBytes(opReader.ReadRawBytes(length.ToIntChecked()));
                        }
                        break;

                    // Flow control
                    case ScriptOp.OP_NOP:
                        {
                        }
                        break;

                    // Stack
                    case ScriptOp.OP_DROP:
                        {
                            if (stack.Count < 1)
                                return false;

                            var value = stack.PopBytes();

                            //logger.LogTrace("{0} dropped {1}", OpName(opByte), value);
                        }
                        break;

                    case ScriptOp.OP_DUP:
                        {
                            if (stack.Count < 1)
                                return false;

                            var value = stack.PeekBytes();
                            stack.PushBytes(value);

                            //logger.LogTrace("{0} duplicated {2}", OpName(opByte), value);
                        }
                        break;

                    // Splice

                    // Bitwise logic
                    case ScriptOp.OP_EQUAL:
                    case ScriptOp.OP_EQUALVERIFY:
                        {
                            if (stack.Count < 2)
                                return false;

                            var value1 = stack.PopBytes();
                            var value2 = stack.PopBytes();

                            var result = value1.SequenceEqual(value2);
                            stack.PushBool(result);

//                            logger.LogTrace(
//@"{0} compared values:
//value1: {1}
//value2: {2}
//result: {3}", OpName(opByte), value1, value2, result);

                            if (op == ScriptOp.OP_EQUALVERIFY)
                            {
                                if (result)
                                    stack.PopBool();
                                else
                                    return false;
                            }
                        }
                        break;

                    // Arithmetic
                    // Note: Arithmetic inputs are limited to signed 32-bit integers, but may overflow their output.

                    // Crypto
                    case ScriptOp.OP_SHA256:
                        {
                            if (stack.Count < 1)
                                return false;

                            var value = stack.PopBytes().ToArray();

                            var hash = Crypto.SingleSHA256(value);
                            stack.PushBytes(hash);

//                            logger.LogTrace(
//@"{0} hashed value:
//value:  {1}
//hash:   {2}", OpName(opByte), value, hash);
                        }
                        break;

                    case ScriptOp.OP_HASH160:
                        {
                            if (stack.Count < 1)
                                return false;

                            var value = stack.PopBytes().ToArray();

                            var hash = Crypto.SingleRIPEMD160(Crypto.SingleSHA256(value));
                            stack.PushBytes(hash);

//                            logger.LogTrace(
//@"{0} hashed value:
//value:  {1}
//hash:   {2}", OpName(opByte), value, hash);
                        }
                        break;

                    case ScriptOp.OP_CHECKSIG:
                    case ScriptOp.OP_CHECKSIGVERIFY:
                        {
                            if (stack.Count < 2)
                                return false;

                            var pubKey = stack.PopBytes().ToArray();
                            var sig = stack.PopBytes().ToArray();

                            var startTime = DateTime.UtcNow;

                            byte hashType; byte[] txSignature, txSignatureHash; BigIntegerBouncy x, y, r, s;
                            var result = VerifySignature(scriptPubKey, tx, sig, pubKey, inputIndex, out hashType, out txSignature, out txSignatureHash, out x, out y, out r, out s);
                            stack.PushBool(result);

                            var finishTime = DateTime.UtcNow;
//                            logger.LogTrace(
//@"{0} executed in {13} ms:
//tx:                 {1}
//inputIndex:         {2}
//pubKey:             {3}
//sig:                {4}
//hashType:           {5}
//txSignature:        {6}
//txSignatureHash:    {7}
//x:                  {8}
//y:                  {9}
//r:                  {10}
//s:                  {11}
//result:             {12}", OpName(opByte), tx.ToRawBytes(), inputIndex, pubKey, sig, hashType, txSignature, txSignatureHash, x, y, r, s, result, (finishTime - startTime).TotalMilliseconds.ToString("0"));

                            if (op == ScriptOp.OP_CHECKSIGVERIFY)
                            {
                                if (result)
                                    stack.PopBool();
                                else
                                    return false;
                            }
                        }
                        break;

                    // Pseudo-words
                    // These words are used internally for assisting with transaction matching. They are invalid if used in actual scripts.

                    // Reserved words
                    // Any opcode not assigned is also reserved. Using an unassigned opcode makes the transaction invalid.

                    default:
                        //OP_PUSHBYTES1-75
                        if (opByte >= (int)ScriptOp.OP_PUSHBYTES1 && opByte <= (int)ScriptOp.OP_PUSHBYTES75)
                        {
                            stack.PushBytes(opReader.ReadRawBytes(opByte));
                            //logger.LogTrace("{0} loaded {1} bytes onto the stack: {2}", OpName(opByte), opByte, stack.PeekBytes());
                        }
                        // Unknown op
                        else
                        {
                            var message = string.Format("Invalid operation in tx {0} input {1}: {2} {3}", tx.Hash.ToHexNumberString(), inputIndex, new[] { opByte }.ToHexNumberString(), OpName(opByte));
                            Debug.WriteLine(message);
                            throw new Exception(message);
                        }
                        break;
                }

                //logger.LogTrace(new string('-', 80));
            }

            //TODO verify no if/else blocks, left over

            // TODO not entirely sure what default return should be
            return true;
        }

        public bool VerifySignature(ImmutableArray<byte> scriptPubKey, Transaction tx, byte[] sig, byte[] pubKey, int inputIndex, out byte hashType, out byte[] txSignature, out byte[] txSignatureHash, out BigIntegerBouncy x, out BigIntegerBouncy y, out BigIntegerBouncy r, out BigIntegerBouncy s)
        {
            // get the 1-byte hashType off the end of sig
            hashType = sig[sig.Length - 1];

            // get the DER encoded portion of sig, which is everything except the last byte (the last byte being hashType)
            var sigDER = sig.Take(sig.Length - 1).ToArray();

            // get the simplified/signing version of the transaction
            txSignature = TxSignature(scriptPubKey, tx, inputIndex, hashType);

            // get the hash of the simplified/signing version of the transaction
            txSignatureHash = Crypto.DoubleSHA256(txSignature);

            // load pubKey
            ReadPubKey(pubKey, out x, out y);
            var publicKeyPoint = curve.Curve.CreatePoint(x, y, withCompression: false);
            var publicKeyParameters = new ECPublicKeyParameters(publicKeyPoint, domainParameters);

            // load sig
            ReadSigKey(sigDER, out r, out s);

            // init signer
            var signer = new ECDsaSigner();
            signer.Init(forSigning: false, parameters: publicKeyParameters);

            // verify that sig is a valid signature from pubKey for the simplified/signing transaction's hash
            var txSignatureHash2 = txSignatureHash;
            var r2 = r;
            var s2 = s;
            //TODO
            var result = BypassVerifySignature || new MethodTimer(false).Time("ECDsa Verify", () => signer.VerifySignature(txSignatureHash2.ToArray(), r2, s2));

            return result;
        }

        private void ReadPubKey(byte[] pubKey, out BigIntegerBouncy x, out BigIntegerBouncy y)
        {
            // public key is encoded as 0x04<x><y>
            // where <x> and <y> are 32-byte unsigned, positive, big-endian integers
            // for a total length of 65 bytes

            if (pubKey.Length != 65 || pubKey[0] != 0x04)
                throw new Exception("TODO wrong public key type");

            x = new BigIntegerBouncy(1, pubKey.ToArray(), 1, 32);
            y = new BigIntegerBouncy(1, pubKey.ToArray(), 33, 32);
        }

        private void ReadSigKey(byte[] sig, out BigIntegerBouncy r, out BigIntegerBouncy s)
        {
            // sig is two DER encoded integers: r and s
            // total length is variable

            using (var stream = new Asn1InputStream(sig.ToArray()))
            {
                var sequence = (DerSequence)stream.ReadObject();
                r = ((DerInteger)sequence[0]).Value;
                s = ((DerInteger)sequence[1]).Value;

                Debug.Assert(sequence.Count == 2);
                //TODO Debug.Assert(sig.SequenceEqual(sequence.GetDerEncoded()));
            }
        }

        public byte[] TxSignature(ImmutableArray<byte> scriptPubKey, Transaction tx, int inputIndex, byte hashType)
        {
            ///TODO
            Debug.Assert(inputIndex < tx.Inputs.Length);

            // Blank out other inputs' signatures
            var empty = ImmutableArray.Create<byte>();
            var newInputs = new TransactionIn[tx.Inputs.Length];
            for (var i = 0; i < tx.Inputs.Length; i++)
            {
                var oldInput = tx.Inputs[i];
                var newInput = oldInput.With(ScriptSignature: i == inputIndex ? scriptPubKey : empty);
                newInputs[i] = newInput;
            }

            //// Blank out some of the outputs
            //if ((hashType & 0x1F) == (int)ScriptHashType.SIGHASH_NONE)
            //{
            //    //TODO
            //    Debug.Assert(false);

            //    // Wildcard payee

            //    // Let the others update at will
            //}
            //else if ((hashType & 0x1F) == (int)ScriptHashType.SIGHASH_SINGLE)
            //{
            //    //TODO
            //    Debug.Assert(false);

            //    // Only lock-in the txout payee at same index as txin

            //    // Let the others update at will
            //}

            //// Blank out other inputs completely, not recommended for open transactions
            //if ((hashType & 0x80) == (int)ScriptHashType.SIGHASH_ANYONECANPAY)
            //{
            //    //TODO
            //    Debug.Assert(false);
            //}

            // return wire-encoded simplified transaction with the 4-byte hashType tacked onto the end
            //var newTx = tx.With(Inputs: newInputsImmutable);
            //var result = new byte[newTx.RawBytes.Count + 4];
            //Buffer.BlockCopy(newTx.RawBytes, 0, result, 0, newTx.RawBytes.Count);
            //Buffer.BlockCopy(Bits.GetBytes((UInt32)hashType), 0, result, newTx.RawBytes.Count, 4);

            // return wire-encoded simplified transaction with the 4-byte hashType tacked onto the end
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            Transaction.WriteRawBytes(writer, tx.Version, newInputs.ToImmutableArray(), tx.Outputs, tx.LockTime);
            writer.Write4Bytes(hashType);

            return stream.ToArray();
        }

        private string OpName(byte op)
        {
            if (op >= (int)ScriptOp.OP_PUSHBYTES1 && op <= (int)ScriptOp.OP_PUSHBYTES75)
            {
                return string.Format("OP_PUSHBYTES{0}", op);
            }
            else
            {
                return Enum.ToObject(typeof(ScriptOp), op).ToString();
            }
        }
    }
}
