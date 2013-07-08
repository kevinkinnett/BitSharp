using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class ValidationException : Exception
    {
        [Obsolete]
        public ValidationException()
            : base()
        { }

        public ValidationException(string message)
            : base(message)
        { }
    }
}
