using System;
using System.Threading;

namespace BitSharp.Common
{
    public class ThrottledNotifyEvent : WaitHandle
    {
        private readonly AutoResetEvent eventHandle;
        private readonly TimeSpan throttleInterval;
        private readonly TimeSpan maxIdleInterval;
        private readonly Timer throttleTimer;

        private bool isSet;
        private DateTime lastSetTime;

        public ThrottledNotifyEvent(bool initialState, TimeSpan throttleInterval)
            : this(initialState, throttleInterval, TimeSpan.MaxValue)
        { }

        public ThrottledNotifyEvent(bool initialState, TimeSpan throttleInterval, TimeSpan maxIdleInterval)
        {
            this.isSet = initialState;
            this.eventHandle = new AutoResetEvent(initialState);

            this.throttleInterval = throttleInterval;
            this.maxIdleInterval = maxIdleInterval;
            this.throttleTimer = new Timer(ThrottleTimerCallback, null, TimeSpan.FromMilliseconds(0), throttleInterval);

            base.SafeWaitHandle = this.eventHandle.SafeWaitHandle;
        }

        public TimeSpan ThrottleInterval { get { return this.throttleInterval; } }

        public TimeSpan MaxIdleInterval { get { return this.maxIdleInterval; } }

        public void Set()
        {
            this.isSet = true;
        }

        public void ForceSet()
        {
            this.eventHandle.Set();
        }

        public override bool WaitOne()
        {
            return this.eventHandle.WaitOne();
        }

        public override bool WaitOne(TimeSpan timeout)
        {
            return this.eventHandle.WaitOne(timeout);
        }

        private void ThrottleTimerCallback(object state)
        {
            if (this.isSet || (DateTime.UtcNow - this.lastSetTime) > this.maxIdleInterval)
            {
                this.isSet = false;
                this.lastSetTime = DateTime.UtcNow;

                this.eventHandle.Set();
            }
        }
    }
}
