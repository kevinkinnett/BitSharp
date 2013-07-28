using BitSharp.Blockchain;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using BitSharp.Network;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Storage.Test;
using System;
using System.Diagnostics;
using System.IO;

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

                    var projectFolder = Environment.CurrentDirectory;
                    while (projectFolder.Contains(@"\bin"))
                        projectFolder = Path.GetDirectoryName(projectFolder);

                    File.Delete(Path.Combine(projectFolder, "Bitcoinj-comparison.log"));

                    var javaProcessStartInfo = new ProcessStartInfo
                        {
                            FileName = @"C:\Program Files\Java\jdk1.7.0_25\bin\java.exe",
                            WorkingDirectory = projectFolder,
                            Arguments = @"-Djava.util.logging.config.file={0}\bitcoinj.log.properties -jar {0}\bitcoinj.jar".Format2(projectFolder),
                            UseShellExecute = false
                        };

                    var javaProcess = Process.Start(javaProcessStartInfo);

                    javaProcess.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
                    Console.ReadLine();
                }
            }
        }
    }
}
