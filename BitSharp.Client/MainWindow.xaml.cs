#define SQLITE

using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain;
using BitSharp.Daemon;
using BitSharp.Node;
using BitSharp.Script;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BitSharp.Network;
using BitSharp.Storage.Firebird;
using BitSharp.Storage.SqlServer;
using BitSharp.Storage.SQLite;

namespace BitSharp.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IStorageContext storageContext;
        private CacheContext cacheContext;
        private IBlockchainRules rules;
        private BlockchainDaemon blockchainDaemon;
        private LocalClient localClient;
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            try
            {
                //File.Delete(@"C:\Users\Paul\AppData\Local\BitSharp\BITSHARP.FDB");

                //TODO
                //MainnetRules.BypassValidation = true;
                MainnetRules.BypassExecuteScript = false;
                ScriptEngine.BypassVerifySignature = true;

#if SQLITE
                var storageContext = new SQLiteStorageContext();
                var knownAddressStorage = new BitSharp.Storage.SQLite.KnownAddressStorage(storageContext);
                this.storageContext = storageContext;
#elif FIREBIRD
                var storageContext = new FirebirdStorageContext();
                var knownAddressStorage = new BitSharp.Storage.Firebird.KnownAddressStorage(storageContext);
                this.storageContext = storageContext;
#elif SQL_SERVER
                var storageContext = new SqlServerStorageContext();
                var knownAddressStorage = new BitSharp.Storage.SqlServer.KnownAddressStorage(storageContext);
                this.storageContext = storageContext;
#endif

                this.cacheContext = new CacheContext(this.storageContext);
                this.rules = new MainnetRules(this.cacheContext);
                this.blockchainDaemon = new BlockchainDaemon(this.rules, this.cacheContext);
                this.localClient = new LocalClient(this.blockchainDaemon, knownAddressStorage);

                // setup view model
                this.viewModel = new MainWindowViewModel(this.blockchainDaemon);

                InitializeComponent();

                // start the blockchain daemon
                this.blockchainDaemon.Start();

                this.viewModel.ViewBlockchainLast();

                // start p2p client
                this.localClient.Start();

                this.DataContext = this.viewModel;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw;
            }
        }

        public MainWindowViewModel ViewModel { get { return this.viewModel; } }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // shutdown
            new IDisposable[]
            {
                this.localClient,
                this.blockchainDaemon,
                this.cacheContext,
                this.storageContext
            }.DisposeList();
        }

        private void ViewFirst_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainFirst();
        }

        private void ViewPrevious_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainPrevious();
        }

        private void ViewNext_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainNext();
        }

        private void ViewLast_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainLast();
        }
    }
}
