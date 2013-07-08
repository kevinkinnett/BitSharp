using BitSharp.Common;
using BitSharp.Script;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Script
{
    public class Stack
    {
        private Stack<ImmutableArray<byte>> stack = new Stack<ImmutableArray<byte>>();

        public int Count { get { return stack.Count; } }

        // Peek
        public ImmutableArray<byte> PeekBytes()
        {
            return stack.Peek();
        }

        public bool PeekBool()
        {
            return CastToBool(stack.Peek());
        }

        public BigInteger PeekBigInteger()
        {
            return CastToBigInteger(stack.Peek());
        }
        
        // Pop
        public ImmutableArray<byte> PopBytes()
        {
            return stack.Pop();
        }

        public bool PopBool()
        {
            return CastToBool(stack.Pop());
        }

        public BigInteger PopBigInteger()
        {
            return CastToBigInteger(stack.Pop());
        }

        // Push
        public void PushBytes(byte[] value)
        {
            stack.Push(value.ToImmutableArray());
        }

        public void PushBytes(ImmutableArray<byte> value)
        {
            stack.Push(value);
        }

        public void PushBool(bool value)
        {
            if (value)
                stack.Push(ImmutableArray.Create((byte)1));
            else
                stack.Push(ImmutableArray.Create<byte>());
        }

        public void PushBigInteger(BigInteger value)
        {
            stack.Push(value.ToByteArray().ToImmutableArray());
        }

        private bool CastToBool(ImmutableArray<byte> value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != 0)
                {
                    // Can be negative zero
                    if (i == value.Length - 1 && value[i] == 0x80)
                        return false;
                    
                    return true;
                }
            }
            
            return false;
        }

        private BigInteger CastToBigInteger(ImmutableArray<byte> value)
        {
            return new BigInteger(value.ToArray());
        }
    }
}
