using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBoundedStorage<TKey, TValue> : IUnboundedStorage<TKey, TValue>
    {
        IEnumerable<TKey> ReadAllKeys();

        IEnumerable<KeyValuePair<TKey, TValue>> ReadAllValues();
    }
}
