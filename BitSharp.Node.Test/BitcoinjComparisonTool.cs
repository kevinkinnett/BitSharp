using BitSharp.Blockchain;
using BitSharp.Daemon;
using BitSharp.Network;
using BitSharp.Node;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Storage.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Node.Test
{
    public class BitcoinjComparisonTool
    {
        public static void Main(string[] args)
        {
            //TODO
            //MainnetRules.BypassValidation = true;
            //MainnetRules.BypassExecuteScript = true;
            ScriptEngine.BypassVerifySignature = true;

            using (var storageContext = new MemoryStorageContext())
            using (var cacheContext = new CacheContext(storageContext))
            {
                var rules = new Testnet2Rules(cacheContext);

                using (var blockchainDaemon = new BlockchainDaemon(rules, cacheContext))
                using (var knownAddressStorage = new MemoryStorage<NetworkAddressKey, NetworkAddressWithTime>(storageContext))
                using (var localClient = new LocalClient(LocalClientType.ComparisonToolTestNet, blockchainDaemon, knownAddressStorage))
                {
                    // start the blockchain daemon
                    blockchainDaemon.Start();

                    // start p2p client
                    localClient.Start();

                    Thread.Sleep(5000);
                }
            }
        }
    }
}
