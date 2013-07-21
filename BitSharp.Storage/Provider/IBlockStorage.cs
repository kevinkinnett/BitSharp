using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockStorage : IBoundedStorage<UInt256, Block>
    {
        IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllBlockHeaders();

        bool TryReadBlockHeader(UInt256 blockHash, out BlockHeader blockHeader);

        IEnumerable<UInt256> FindMissingPreviousBlocks();

        IEnumerable<BlockHeader> FindByPreviousBlockHash(UInt256 previousBlockHash);
    }
}
