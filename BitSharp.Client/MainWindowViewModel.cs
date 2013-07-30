using BitSharp.Blockchain;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using BitSharp.Data;
using BitSharp.Storage;
using BitSharp.Storage.ExtensionMethods;
using BitSharp.Network;
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

        private Data.Blockchain viewBlockchain;

        private long _winningBlockchainHeight;
        private long _currentBlockchainHeight;
        private long _downloadedBlockCount;

        public MainWindowViewModel(BlockchainDaemon blockchainDaemon)
        {
            this.blockchainDaemon = blockchainDaemon;

            var winningBlockLocal = this.blockchainDaemon.WinningBlock;
            var currentBlockchainLocal = this.blockchainDaemon.CurrentBlockchain;

            this.viewBlockchain = currentBlockchainLocal;

            this.WinningBlockchainHeight = winningBlockLocal.Height;
            this.CurrentBlockchainHeight = currentBlockchainLocal.Height;
            this.DownloadedBlockCount = this.blockchainDaemon.CacheContext.BlockCache.Count;

            this.blockchainDaemon.CacheContext.BlockCache.OnAddition +=
                blockHash =>
                    DownloadedBlockCount = this.blockchainDaemon.CacheContext.BlockCache.Count;

            this.blockchainDaemon.OnWinningBlockChanged +=
                (sender, block) =>
                    WinningBlockchainHeight = block.Height;

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

        public long DownloadedBlockCount
        {
            get { return this._downloadedBlockCount; }
            set { SetValue(ref this._downloadedBlockCount, value); }
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

                var height = value;
                if (height > currentBlockchain.Height)
                    height = currentBlockchain.Height;

                var targetBlock = currentBlockchain.BlockList[(int)height];
                SetViewBlockchain(targetBlock);
            }
        }

        public IList<TxOutputKey> ViewBlockchainSpendOutputs { get; protected set; }

        public IList<TxOutputKey> ViewBlockchainReceiveOutputs { get; protected set; }

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

        private void SetViewBlockchain(ChainedBlock targetBlock)
        {
            using (var cancelToken = new CancellationTokenSource())
            {
                try
                {
                    List<MissingDataException> missingData;
                    var blockchain = this.blockchainDaemon.Calculator.CalculateBlockchainFromExisting(this.viewBlockchain, targetBlock, out missingData, cancelToken.Token);
                    SetViewBlockchain(blockchain);
                }
                catch (MissingDataException)
                {
                    // TODO
                }
                catch (AggregateException e)
                {
                    if (!e.IsMissingDataOnly())
                        throw;
                }
            }
        }

        private void SetViewBlockchain(Data.Blockchain blockchain)
        {
            this.viewBlockchain = blockchain;

            try
            {
                if (blockchain.Height > 0)
                {
                    Block block;
                    if (this.blockchainDaemon.TryGetBlock(this.viewBlockchain.RootBlockHash, out block))
                    {
                        // TODO this is abusing rollback a bit just to get the transactions that exist in a target block that's already known
                        // TODO make a better api for get the net output of a block
                        List<TxOutputKey> spendOutputs, receiveOutputs;
                        this.blockchainDaemon.Calculator.RollbackBlockchain(this.viewBlockchain, block, out spendOutputs, out receiveOutputs);

                        ViewBlockchainSpendOutputs = spendOutputs;
                        ViewBlockchainReceiveOutputs = receiveOutputs;
                    }
                }
                else
                {
                    ViewBlockchainSpendOutputs = new List<TxOutputKey>();
                    ViewBlockchainReceiveOutputs = new List<TxOutputKey>();
                }
            }
            catch (MissingDataException)
            {
                // TODO
            }
            catch (AggregateException e)
            {
                if (!e.IsMissingDataOnly())
                    throw;
            }

            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs("ViewBlockchainHeight"));
                handler(this, new PropertyChangedEventArgs("ViewBlockchainSpendOutputs"));
                handler(this, new PropertyChangedEventArgs("ViewBlockchainReceiveOutputs"));
            }
        }

        public long BlockCacheSizeMB
        {
            get
            {
                return this.blockchainDaemon.CacheContext.BlockCache.MaxCacheMemorySize / 1.MILLION();
            }
            set
            {
                var newValue = value * 1.MILLION();
                if (newValue != this.blockchainDaemon.CacheContext.BlockCache.MaxCacheMemorySize)
                {
                    this.blockchainDaemon.CacheContext.BlockCache.MaxCacheMemorySize = newValue;
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
                return this.blockchainDaemon.CacheContext.BlockHeaderCache.MaxCacheMemorySize / 1.MILLION();
            }
            set
            {
                var newValue = value * 1.MILLION();
                if (newValue != this.blockchainDaemon.CacheContext.BlockHeaderCache.MaxCacheMemorySize)
                {
                    this.blockchainDaemon.CacheContext.BlockHeaderCache.MaxCacheMemorySize = newValue;
                    var handler = this.PropertyChanged;
                    if (handler != null)
                        handler(this, new PropertyChangedEventArgs("HeaderCacheSizeMB"));
                }
            }
        }

        public long ChainedBlockCacheSizeMB
        {
            get
            {
                return this.blockchainDaemon.CacheContext.ChainedBlockCache.MaxCacheMemorySize / 1.MILLION();
            }
            set
            {
                var newValue = value * 1.MILLION();
                if (newValue != this.blockchainDaemon.CacheContext.ChainedBlockCache.MaxCacheMemorySize)
                {
                    this.blockchainDaemon.CacheContext.ChainedBlockCache.MaxCacheMemorySize = newValue;
                    var handler = this.PropertyChanged;
                    if (handler != null)
                        handler(this, new PropertyChangedEventArgs("ChainedBlockSizeMB"));
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
