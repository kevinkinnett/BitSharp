using BitSharp.Common;
using BitSharp.Storage;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    public class MemoryBlockHeaderStorage : IBlockHeaderStorage
    {
        private readonly MemoryBlockDataStorage blockStorage;

        public MemoryBlockHeaderStorage(MemoryBlockDataStorage blockStorage)
        {
            this.blockStorage = blockStorage;
        }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.blockStorage.ReadAllKeys();
        }

        public new IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllValues()
        {
            foreach (var keyPair in this.blockStorage.ReadAllValues())
            {
                yield return new KeyValuePair<UInt256, BlockHeader>(keyPair.Key, keyPair.Value.Header);
            }
        }

        public bool TryReadValue(UInt256 key, out BlockHeader value)
        {
            Block block;
            if (this.blockStorage.TryReadValue(key, out block))
            {
                value = block.Header;
                return true;
            }
            else
            {
                value = default(BlockHeader);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BlockHeader>>> values)
        {
            throw new NotSupportedException();
        }
    }
}
