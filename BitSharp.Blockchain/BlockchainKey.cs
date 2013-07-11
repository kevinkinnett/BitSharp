
using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public struct BlockchainKey
    {
        private readonly string _filePath;
        private readonly Guid _guid;
        private readonly UInt256 _rootBlockHash;

        public BlockchainKey(string filePath, Guid guid, UInt256 rootBlockHash)
        {
            this._filePath = filePath;
            this._guid = guid;
            this._rootBlockHash = rootBlockHash;
        }

        public string FilePath { get { return this._filePath; } }

        public Guid Guid { get { return this._guid; } }

        public UInt256 RootBlockHash { get { return this._rootBlockHash; } }
    }
}
