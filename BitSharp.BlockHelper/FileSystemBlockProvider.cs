using BitSharp.Data;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.BlockHelper
{
    public class FileSystemBlockProvider : BlockProvider
    {
        private static readonly ReaderWriterLock rwl = new ReaderWriterLock();
        private static readonly object staticLock = new object();

        private readonly string storagePath;
        private readonly string indexStoragePath;
        private readonly string hashStoragePath;
        private readonly BlockExplorerProvider provider = new BlockExplorerProvider();

        public FileSystemBlockProvider()
        {
            storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp/storage");
            indexStoragePath = Path.Combine(storagePath, "index");
            hashStoragePath = Path.Combine(storagePath, "hash");
        }

        public override Block GetBlock(int index)
        {
            var path = Path.Combine(indexStoragePath, string.Format("{0}.txt", index));
            return GetCachedBlock(path, () => provider.GetBlockJson(index));
        }

        public override Block GetBlock(string hash)
        {
            var path = Path.Combine(hashStoragePath, string.Format("{0}.txt", hash));
            return GetCachedBlock(path, () => provider.GetBlockJson(hash));
        }

        public Block GetCachedBlock(string path, Func<string> getBlockJson)
        {
            var file = new FileInfo(path);
            if (file.Exists)
            {
                rwl.AcquireReaderLock(1000);
                try
                {
                    using (var reader = new StreamReader(file.FullName, Encoding.UTF8))
                    {
                        return BlockJson.GetBlockFromJson(reader.ReadToEnd());
                    }
                }
                finally
                {
                    rwl.ReleaseLock();
                }
            }
            else
            {
                var blockJson = getBlockJson();

                file.Directory.Create();

                lock (staticLock)
                {
                    rwl.AcquireWriterLock(1000);
                    try
                    {
                        using (var writer = new StreamWriter(file.FullName, false, Encoding.UTF8))
                        {
                            writer.Write(blockJson);
                        }
                    }
                    finally
                    {
                        rwl.ReleaseLock();
                    }
                }

                return BlockJson.GetBlockFromJson(blockJson);
            }
        }

    }
}
