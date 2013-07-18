using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct BlockchainKey
    {
        private readonly Guid _guid;
        private readonly UInt256 _rootBlockHash;

        public BlockchainKey(Guid guid, UInt256 rootBlockHash)
        {
            this._guid = guid;
            this._rootBlockHash = rootBlockHash;
        }

        public Guid Guid { get { return this._guid; } }

        public UInt256 RootBlockHash { get { return this._rootBlockHash; } }
    }
}
