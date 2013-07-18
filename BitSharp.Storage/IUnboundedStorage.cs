using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IUnboundedStorage<TKey, TValue> : IDisposable
    {
        bool TryReadValue(TKey key, out TValue value);
        
        bool TryWriteValues(IEnumerable<KeyValuePair<TKey, WriteValue<TValue>>> values);
    }
}
