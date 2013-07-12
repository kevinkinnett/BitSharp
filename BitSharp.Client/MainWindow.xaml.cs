using BitSharp.Blockchain;
using BitSharp.Daemon;
using BitSharp.Database;
using BitSharp.Node;
using BitSharp.Script;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace BitSharp.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BlockDataStorage blockDataStorage;
        private BlockHeaderStorage blockHeaderStorage;
        private BlockMetadataStorage blockMetadataStorage;
        private TransactionStorage txStorage;
        private IBlockchainRules rules;
        private StorageManager storageManager;
        private BlockchainDaemon blockchainDaemon;
        private LocalClient localClient;
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            try
            {
                //TODO
                MainnetRules.BypassValidation = true;
                MainnetRules.BypassExecuteScript = false;
                ScriptEngine.BypassVerifySignature = true;

                this.blockDataStorage = new BlockDataStorage();
                this.blockHeaderStorage = new BlockHeaderStorage();
                this.blockMetadataStorage = new BlockMetadataStorage();
                this.txStorage = new TransactionStorage();

                //blockDataStorage.Truncate();
                //blockMetadataStorage.Truncate();
                //new KnownAddressStorage().Truncate();

                this.rules = new MainnetRules();
                this.storageManager = new StorageManager(this.blockDataStorage, this.blockHeaderStorage, this.blockMetadataStorage, this.txStorage);
                this.blockchainDaemon = new BlockchainDaemon(this.rules, this.storageManager);
                this.localClient = new LocalClient(this.blockchainDaemon);

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
            this.localClient.Dispose();
            this.blockchainDaemon.Dispose();
            this.storageManager.Dispose();
            this.blockDataStorage.Dispose();
            this.blockHeaderStorage.Dispose();
            this.blockMetadataStorage.Dispose();
            this.txStorage.Dispose();
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
