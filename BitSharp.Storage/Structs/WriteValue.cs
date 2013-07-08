using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public struct WriteValue<T>
    {
        public readonly T Value;
        public readonly bool IsCreate;
        public readonly Guid Guid;

        public WriteValue(T Value, bool IsCreate)
        {
            this.Value = Value;
            this.IsCreate = IsCreate;
            this.Guid = Guid.NewGuid();
        }
    }
}
