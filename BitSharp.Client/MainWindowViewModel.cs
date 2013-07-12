using BitSharp.Blockchain;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BitSharp.Client
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly BlockchainDaemon blockchainDaemon;

        private Blockchain.Blockchain viewBlockchain;

        private long _winningBlockchainHeight;
        private long _currentBlockchainHeight;

        public MainWindowViewModel(BlockchainDaemon blockchainDaemon)
        {
            this.blockchainDaemon = blockchainDaemon;
            this.viewBlockchain = this.blockchainDaemon.CurrentBlockchain;

            this.blockchainDaemon.OnWinningBlockChanged +=
                (sender, block) =>
                    WinningBlockchainHeight = block.Height ?? -1;

            this.blockchainDaemon.OnCurrentBlockchainChanged +=
                (sender, blockchain) =>
                    CurrentBlockchainHeight = blockchain.Height;
        }

        public long WinningBlockchainHeight
        {
            get { return this._winningBlockchainHeight; }
            set { SetValue(ref this._winningBlockchainHeight, value); }
        }

        public long CurrentBlockchainHeight
        {
            get { return this._currentBlockchainHeight; }
            set { SetValue(ref this._currentBlockchainHeight, value); }
        }

        public long ViewBlockchainHeight
        {
            get
            {
                return this.viewBlockchain.Height;
            }
            set
            {
                var currentBlockchain = this.blockchainDaemon.CurrentBlockchain;
                if (currentBlockchain.BlockList.Count == 0)
                    return;

                var height = value;
                if (height > currentBlockchain.Height)
                    height = currentBlockchain.Height;

                var targetBlock = currentBlockchain.BlockList[(int)height];
                SetViewBlockchain(targetBlock);
            }
        }

        public IList<TxOutputKey> ViewBlockchainUtxoDelta { get; protected set; }

        public void ViewBlockchainFirst()
        {
            var currentBlockchain = this.blockchainDaemon.CurrentBlockchain;
            if (currentBlockchain.BlockList.Count == 0)
                return;

            var targetBlock = currentBlockchain.BlockList.First();
            SetViewBlockchain(targetBlock);
        }

        public void ViewBlockchainPrevious()
        {
            var currentBlockchain = this.blockchainDaemon.CurrentBlockchain;
            if (currentBlockchain.BlockList.Count == 0)
                return;

            var height = this.viewBlockchain.Height - 1;
            if (height > currentBlockchain.Height)
                height = currentBlockchain.Height;

            var targetBlock = currentBlockchain.BlockList[height];
            SetViewBlockchain(targetBlock);
        }

        public void ViewBlockchainNext()
        {
            var currentBlockchain = this.blockchainDaemon.CurrentBlockchain;
            if (currentBlockchain.BlockList.Count == 0)
                return;

            var height = this.viewBlockchain.Height + 1;
            if (height > currentBlockchain.Height)
                height = currentBlockchain.Height;

            var targetBlock = currentBlockchain.BlockList[height];
            SetViewBlockchain(targetBlock);
        }

        public void ViewBlockchainLast()
        {
            SetViewBlockchain(this.blockchainDaemon.CurrentBlockchain);
        }

        private void SetViewBlockchain(BlockMetadata targetBlock)
        {
            using (var cancelToken = new CancellationTokenSource())
            {
                var blockchain = this.blockchainDaemon.Calculator.CalculateBlockchainFromExisting(this.viewBlockchain, targetBlock, cancelToken.Token);
                SetViewBlockchain(blockchain);
            }
        }

        private void SetViewBlockchain(Blockchain.Blockchain blockchain)
        {
            this.viewBlockchain = blockchain;

            if (blockchain.Height > 0)
            {
                List<TxOutputKey> spendOutputs, receiveOutputs;
                this.blockchainDaemon.Calculator.RollbackBlockchain(this.viewBlockchain, out spendOutputs, out receiveOutputs);

                spendOutputs.AddRange(receiveOutputs);
                ViewBlockchainUtxoDelta = spendOutputs;
            }
            else
            {
                ViewBlockchainUtxoDelta = new List<TxOutputKey>();
            }

            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs("ViewBlockchainHeight"));
                handler(this, new PropertyChangedEventArgs("ViewBlockchainUtxoDelta"));
            }
        }

        public long BlockCacheSizeMB
        {
            get
            {
                return this.blockchainDaemon.StorageManager.BlockDataCache.MaxCacheMemorySize / 1.MILLION();
            }
            set
            {
                var newValue = value * 1.MILLION();
                if (newValue != this.blockchainDaemon.StorageManager.BlockDataCache.MaxCacheMemorySize)
                {
                    this.blockchainDaemon.StorageManager.BlockDataCache.MaxCacheMemorySize = newValue;
                    var handler = this.PropertyChanged;
                    if (handler != null)
                        handler(this, new PropertyChangedEventArgs("BlockCacheSizeMB"));
                }
            }
        }

        public long HeaderCacheSizeMB
        {
            get
            {
                return this.blockchainDaemon.StorageManager.BlockHeaderCache.MaxCacheMemorySize / 1.MILLION();
            }
            set
            {
                var newValue = value * 1.MILLION();
                if (newValue != this.blockchainDaemon.StorageManager.BlockHeaderCache.MaxCacheMemorySize)
                {
                    this.blockchainDaemon.StorageManager.BlockHeaderCache.MaxCacheMemorySize = newValue;
                    var handler = this.PropertyChanged;
                    if (handler != null)
                        handler(this, new PropertyChangedEventArgs("HeaderCacheSizeMB"));
                }
            }
        }

        public long MetadataCacheSizeMB
        {
            get
            {
                return this.blockchainDaemon.StorageManager.BlockMetadataCache.MaxCacheMemorySize / 1.MILLION();
            }
            set
            {
                var newValue = value * 1.MILLION();
                if (newValue != this.blockchainDaemon.StorageManager.BlockMetadataCache.MaxCacheMemorySize)
                {
                    this.blockchainDaemon.StorageManager.BlockMetadataCache.MaxCacheMemorySize = newValue;
                    var handler = this.PropertyChanged;
                    if (handler != null)
                        handler(this, new PropertyChangedEventArgs("MetadataCacheSizeMB"));
                }
            }
        }

        private void SetValue<T>(ref T currentValue, T newValue, [CallerMemberName] string propertyName = "") where T : IEquatable<T>
        {
            if (!currentValue.Equals(newValue))
            {
                currentValue = newValue;

                var handler = this.PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
