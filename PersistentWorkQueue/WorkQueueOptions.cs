using System;

namespace PersistentWorkQueue
{
    public record WorkQueueOptions
    {
        public bool FireAndForget { get; init; } = true;
        public TimeSpan TimerInterval {  get; init;} = TimeSpan.FromSeconds(30);
    }
}
