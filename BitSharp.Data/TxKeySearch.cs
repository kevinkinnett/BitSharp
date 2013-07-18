using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct TxKeySearch
    {
        private readonly UInt256 _txHash;
        private readonly ImmutableHashSet<UInt256> _blockHashes;

        public TxKeySearch(UInt256 txHash, ImmutableHashSet<UInt256> blockHashes)
        {
            this._txHash = txHash;
            this._blockHashes = blockHashes;
        }

        public UInt256 TxHash { get { return this._txHash; } }

        public ImmutableHashSet<UInt256> BlockHashes { get { return this._blockHashes; } }
    }
}
