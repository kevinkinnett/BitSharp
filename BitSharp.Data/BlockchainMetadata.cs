using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct BlockchainMetadata
    {
        private readonly Guid _guid;
        private readonly UInt256 _rootBlockHash;
        private readonly BigInteger _totalWork;

        public BlockchainMetadata(Guid guid, UInt256 rootBlockHash, BigInteger totalWork)
        {
            this._guid = guid;
            this._rootBlockHash = rootBlockHash;
            this._totalWork = totalWork;
        }

        public Guid Guid { get { return this._guid; } }

        public UInt256 RootBlockHash { get { return this._rootBlockHash; } }

        public BigInteger TotalWork { get { return this._totalWork; } }
    }
}
