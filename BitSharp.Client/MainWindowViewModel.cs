using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Client
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly BlockchainDaemon blockchainDaemon;

        private long _winningBlockchainHeight;
        private long _currentBlockchainHeight;

        public MainWindowViewModel(BlockchainDaemon blockchainDaemon)
        {
            this.blockchainDaemon = blockchainDaemon;

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
