using BitSharp.Common;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    //TODO move methods into IBlockDataStorage

    public interface IBlockHeaderStorage : /*TODO IReadOnly*/ IDataStorage<UInt256, BlockHeader>
    {
    }
}
