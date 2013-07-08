using BitSharp.Common;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public interface IBlockchainRetriever
    {
        bool TryGetBlock(UInt256 blockHash, out Block block, bool saveInCache = true);

        bool TryGetBlockHeader(UInt256 blockHash, out BlockHeader blockHeader, bool saveInCache = true);

        bool TryGetBlockMetadata(UInt256 blockHash, out BlockMetadata blockMetadata, bool saveInCache = true);

        bool TryGetTransaction(UInt256 transactionHash, out Transaction transaction, bool saveInCache = true);

        //TODO
        long BlockCacheMemorySize { get; }

        long HeaderCacheMemorySize { get; }

        long MetadataCacheMemorySize { get; }
    }
}
