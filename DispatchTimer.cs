using System;
using CoreFoundation;
using Foundation;

namespace Xamarin.iOS.EddystoneScanner
{
    using TimerHandler = Action<DispatchTimer>;

    public class DispatchTimer : NSObject
    {
        readonly long delay;
        readonly DispatchQueue queue;
        readonly TimerHandler timerBlock;

        Action wrappedBlock;
        DispatchSource.Timer source;

        public DispatchTimer(long delay, DispatchQueue queue, TimerHandler block)
        {
            this.timerBlock = block;
            this.queue = queue;
            this.delay = delay;
            this.source = new DispatchSource.Timer(queue);

            base.Init();

            Action wrapper = () =>
            {
                if (!source.IsCanceled)
                {
                    source.Cancel();
                    timerBlock(this);
                }
            };

            wrappedBlock = wrapper;
        }

        public void Schedule()
        {
            this.Reschedule();
            this.source.SetEventHandler(handler: this.wrappedBlock);
            this.source.Resume();
        }

        public void Reschedule()
        {
            this.source.SetTimer(new DispatchTime(DispatchTime.Now, delay), 0, 0);
        }

        public void Suspend()
        {
            this.source.Suspend();
        }

        public void Resume()
        {
            this.source.Resume();
        }

        public void Cancel()
        {
            this.source.Cancel();
        }

        public static DispatchTimer ScheduledDispatchTimer(long delay, DispatchQueue queue, TimerHandler block) {
            var dt = new DispatchTimer(delay: delay, queue: queue, block: block);
            dt.Schedule();

            return dt;
        }

    }
}
