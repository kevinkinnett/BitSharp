using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using BitSharp.WireProtocol;
using BitSharp.Common;

namespace BitSharp.Blockchain
{
    public static class Miner
    {
        private struct LocalMinerState
        {
            public readonly byte[] headerBytes;
            public long total;
            public readonly SHA256 sha256;

            public LocalMinerState(byte[] headerBytes)
            {
                this.headerBytes = (byte[])headerBytes.Clone();
                this.total = 0;
                this.sha256 = SHA256.Create();
            }
        }

        public static BlockHeader? MineBlockHeader(BlockHeader blockHeader, UInt256 hashTarget)
        {
            var blockHeaderBytes = blockHeader.ToRawBytes();
            var hashTargetBytes = hashTarget.ToByteArray();

            var start = 0;
            var finish = UInt32.MaxValue;
            var total = 0L;
            var nonceIndex = 76;
            var minedNonce = (UInt32?)null;

            //Debug.WriteLine("Starting mining: {0}".Format2(DateTime.Now.ToString("hh:mm:ss")));

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Parallel.For(
                start, finish,
                () => new LocalMinerState(blockHeaderBytes),
                (nonceLong, loopState, localState) =>
                {
                    localState.total++;

                    var nonce = (UInt32)nonceLong;
                    var nonceBytes = Bits.GetBytes(nonce);
                    Buffer.BlockCopy(nonceBytes, 0, localState.headerBytes, nonceIndex, 4);

                    var headerBytes = localState.headerBytes;
                    var sha256 = localState.sha256;
                    var hashBytes = sha256.ComputeHash(sha256.ComputeHash(headerBytes));

                    if (BytesCompareLE(hashBytes, hashTargetBytes) < 0)
                    {
                        minedNonce = nonce;
                        loopState.Break();
                    }

                    return localState;
                },
                localState => { Interlocked.Add(ref total, localState.total); });

            stopwatch.Stop();

            var hashRate = ((float)total / 1000 / 1000) / ((float)stopwatch.ElapsedMilliseconds / 1000);

            if (minedNonce != null)
            {
                //Debug.WriteLine("Found block in {0} hh:mm:ss at Nonce {1}, Hash Rate: {2} mHash/s, Total Hash Attempts: {3}, Found Hash: {4}".Format2(stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), minedNonce, hashRate, total.ToString("#,##0"), blockHeader.With(Nonce: minedNonce).Hash));
                return blockHeader.With(Nonce: minedNonce);
            }
            else
            {
                //Debug.WriteLine("No block found in {0} hh:mm:ss, Hash Rate: {1} mHash/s, Total Hash Attempts: {2}, Found Hash: {3}".Format2(stopwatch.Elapsed.ToString(@"hh\:mm\:ss"), hashRate, total.ToString("#,##0"), blockHeader.With(Nonce: minedNonce).Hash));
                return null;
            }
        }

        private static int BytesCompareLE(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException();

            for (var i = a.Length - 1; i >= 0; i--)
            {
                if (a[i] < b[i])
                    return -1;
                else if (a[i] > b[i])
                    return +1;
            }

            return 0;
        }
    }
}
