using BitSharp.Common;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockDataStorage : IDataStorage<UInt256, Block>
    {
    }
}
