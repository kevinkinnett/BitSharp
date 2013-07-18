using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class Worker : IDisposable
    {
        private readonly Action workAction;

        private readonly CancellationTokenSource shutdownToken;

        private readonly Thread workerThread;
        private readonly AutoResetEvent notifyEvent;
        private readonly AutoResetEvent forceNotifyEvent;
        private readonly ManualResetEventSlim idleEvent;
        private readonly ManualResetEventSlim stopEvent;

        private readonly SemaphoreSlim semaphore;
        private bool isStarted;
        private bool isDisposed;

        public Worker(string name, Action workAction, bool runOnStart, TimeSpan waitTime, TimeSpan maxIdleTime)
        {
            this.Name = name;
            this.workAction = workAction;
            this.WaitTime = waitTime;
            this.MaxIdleTime = maxIdleTime;

            this.shutdownToken = new CancellationTokenSource();

            this.workerThread = new Thread(WorkerLoop);
            this.notifyEvent = new AutoResetEvent(runOnStart);
            this.forceNotifyEvent = new AutoResetEvent(runOnStart);
            this.idleEvent = new ManualResetEventSlim(false);
            this.stopEvent = new ManualResetEventSlim(true);

            this.semaphore = new SemaphoreSlim(1);
            this.isStarted = false;
            this.isDisposed = false;
        }

        public string Name { get; protected set; }

        public TimeSpan WaitTime { get; set; }

        public TimeSpan MaxIdleTime { get; set; }

        public void Start()
        {
            if (!this.isStarted)
            {
                this.semaphore.Do(() =>
                {
                    if (!this.isStarted)
                    {
                        this.workerThread.Start();
                        this.isStarted = true;
                    }
                });
            }

            this.stopEvent.Set();
        }

        public void Stop()
        {
            this.semaphore.Do(() =>
            {
                this.stopEvent.Reset();
            });
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                try
                {
                    this.semaphore.Do(() =>
                    {
                        if (this.isStarted && !this.isDisposed)
                        {
                            this.notifyEvent.Set();
                            this.forceNotifyEvent.Set();
                            this.shutdownToken.Cancel();

                            this.workerThread.Join(5000);

                            this.shutdownToken.Dispose();
                            this.notifyEvent.Dispose();
                            this.forceNotifyEvent.Dispose();
                            this.idleEvent.Dispose();
                            this.stopEvent.Dispose();

                            this.isStarted = false;
                            this.isDisposed = true;
                        }
                    });
                    this.semaphore.Dispose();
                }
                catch (Exception) { }
            }
        }

        public void NotifyWork()
        {
            this.notifyEvent.Set();
        }

        public void ForceWork()
        {
            this.forceNotifyEvent.Set();
            this.notifyEvent.Set();
        }

        public void ForceWorkAndWait()
        {
            // wait for worker to idle
            this.idleEvent.Wait();

            // reset its idle state
            this.idleEvent.Reset();

            // force an execution
            ForceWork();

            // wait for worker to be idle again
            this.idleEvent.Wait();
        }

        private void WorkerLoop()
        {
            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // notify worker is idle
                    this.idleEvent.Set();

                    // stop execution if requested
                    this.stopEvent.Wait();

                    // delay for the requested wait time, unless forced
                    this.forceNotifyEvent.WaitOne(this.WaitTime);

                    // wait for work notification
                    this.notifyEvent.WaitOne(this.MaxIdleTime - this.WaitTime); // subtract time already spent waiting

                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // notify that work is starting
                    this.idleEvent.Reset();

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // perform the work
                    workAction();

                    stopwatch.Stop();
                    //Debug.WriteLine("{0} worked in {1:#,##0.000}s".Format2(this.Name, stopwatch.EllapsedSecondsFloat()));
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
