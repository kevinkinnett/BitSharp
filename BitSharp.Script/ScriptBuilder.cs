
using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Script
{
    public class ScriptBuilder
    {
        private readonly MemoryStream stream;

        public ScriptBuilder()
        {
            this.stream = new MemoryStream();
        }

        public byte[] GetScript()
        {
            return this.stream.ToArray();
        }

        public void WriteOp(ScriptOp op)
        {
            stream.WriteByte((byte)op);
        }

        public void WritePushData(byte[] data)
        {
            checked
            {
                if (data.Length <= (int)ScriptOp.OP_PUSHBYTES75)
                {
                    stream.WriteByte((byte)data.Length);
                    stream.Write(data, 0, data.Length);
                }
                else if (data.Length < 0x100)
                {
                    WriteOp(ScriptOp.OP_PUSHDATA1);
                    stream.WriteByte((byte)data.Length);
                    stream.Write(data, 0, data.Length);
                }
                else if (data.Length < 0x10000)
                {
                    WriteOp(ScriptOp.OP_PUSHDATA2);
                    stream.Write(Bits.GetBytes((UInt16)data.Length), 0, 2);
                    stream.Write(data, 0, data.Length);
                }
                else if (data.LongLength < 0x100000000L)
                {
                    WriteOp(ScriptOp.OP_PUSHDATA4);
                    stream.Write(Bits.GetBytes((UInt32)data.Length), 0, 4);
                    stream.Write(data, 0, data.Length);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("data");
                }
            }
        }
    }
}
