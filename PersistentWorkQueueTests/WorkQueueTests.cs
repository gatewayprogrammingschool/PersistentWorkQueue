using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using PersistentWorkQueue;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PersistentWorkQueue.Tests
{
    [TestClass()]
    public class WorkQueueTests
    {
        private WorkQueueOptions _noTimerOptions = new WorkQueueOptions()
        {
           TimerInterval=TimeSpan.Zero,
           FireAndForget=false
        };

        private WorkQueueOptions _timerOptions = new WorkQueueOptions()
        {
           TimerInterval=TimeSpan.FromSeconds(30),
           FireAndForget=false
        };

        private Action<object> _testAction = o => Console.WriteLine($"_testAction: {o}");
        private void TestDelegate(object o) => Console.WriteLine($"TestDelegate: {o}");
        private void TestEventHandler(object o) => Console.WriteLine($"TestEventHandler: {o}");

        [TestMethod()]
        public void CreateActionWorkQueue()
        {
            var q = new WorkQueue<object>(_testAction, _noTimerOptions);

            q.Should().NotBeNull();
            q.Options.Should().NotBeNull();
            q.Options.Should().Be(_noTimerOptions);
            q.Action.Should().Be(_testAction);
        }

        [TestMethod()]
        public void CreateDelegateWorkQueue()
        {
            var q = new WorkQueue<object>(TestDelegate, _noTimerOptions);

            q.Should().NotBeNull();
            q.Options.Should().NotBeNull();
            q.Options.Should().Be(_noTimerOptions);
        }

        [TestMethod()]
        public void CreateEventWorkQueue()
        {
            var q = new WorkQueue<object>(QueueTypes.Event, _noTimerOptions);

            q.Should().NotBeNull();
            q.QueueType.Should().Be(QueueTypes.Event);
            q.Options.Should().NotBeNull();
            q.Options.Should().Be(_noTimerOptions);
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(1, 2, 3)]
        public async Task AddWorkForAction(params int[] values)
        {
            Exception ex = null;
            await Task.Run(() =>
            {
                var q = new WorkQueue<ResettablePair>(_testAction, _noTimerOptions);

                q.OnCompleted += MreOnCompleted2;

#pragma warning disable PH_P012 // Prefer Slim Synchronization
                var wrappers = q.Enqueue(values.Select(v => new ResettablePair(v, new ManualResetEvent(false))))
                    .ToList();
#pragma warning restore PH_P012 // Prefer Slim Synchronization

                wrappers.Count.Should().Be(values.Length);

                var mres = wrappers.Select(w => w.Request.Mre).ToArray();

                ManualResetEvent.WaitAll(mres).Should().BeTrue();

                wrappers.ForEach(wrapper =>
                {
                    wrapper.Id.Should().NotBe(default(Guid));

                    wrapper.Attempts.Should().BeEmpty();

                    q.Succeeded.First(w => w.Id == wrapper.Id).Should().NotBeNull();
                });
            }).ContinueWith(task =>
            {
                if (task.Exception is not null)
                {
                    ex = task.Exception;
                }
            });

            ex.Should().BeNull(ex?.ToString());
        }

        [TestMethod()]
        public void AddWorkForDelegate()
        {
            ManualResetEvent mre = new (false);
            var q = new WorkQueue<ManualResetEvent>(TestDelegate, _noTimerOptions);

            q.OnCompleted += MreOnCompleted;

            var wrapper = q.Enqueue(mre);

            wrapper.Id.Should().NotBe(default(Guid));

            mre.WaitOne();

            wrapper.Attempts.Should().BeEmpty();

            q.Succeeded.Any(w => w.Id == wrapper.Id).Should().BeTrue();
        }

        [TestMethod()]
        public void AddWorkForEvent()
        {
            ManualResetEvent mre = new (false);
            var q = new WorkQueue<ManualResetEvent>(QueueTypes.Event, _noTimerOptions);

            q.OnAction += TestEventHandler;
            q.OnCompleted += MreOnCompleted;

            var wrapper = q.Enqueue(mre);

            wrapper.Id.Should().NotBe(default(Guid));

            mre.WaitOne();

            wrapper.Attempts.Should().BeEmpty();

            q.Succeeded.Any(w => w.Id == wrapper.Id).Should().BeTrue();
        }

        private void MreOnCompleted(RequestWrapper<ManualResetEvent> wrapper)
        {
            wrapper.Request.Set();
        }

        private void MreOnCompleted2(RequestWrapper<ResettablePair> wrapper)
        {
            Console.WriteLine(wrapper.Request.Value);
            wrapper.Request.Mre.Set();
        }

        [TestMethod()]
        public void StartTimerTest()
        {
            var q = new WorkQueue<ManualResetEvent>(_testAction, _timerOptions);

            q.IsRunning.Should().BeTrue();

            q.StopTimer();

            q.IsRunning.Should().BeFalse();

            q.StartTimer();

            q.IsRunning.Should().BeTrue();
        }

        [TestMethod()]
        public void StopTimerTest()
        {
            var q = new WorkQueue<ManualResetEvent>(_testAction, _timerOptions);

            q.IsRunning.Should().BeTrue();

            q.StopTimer();

            q.IsRunning.Should().BeFalse();
        }

        [TestMethod()]
        public void DoWorkTest()
        {
            int counter = 0;

            Action<ManualResetEvent> flipFlop = mre => {
                if(counter++ % 2 == 1) mre.Set(); 
                else throw new Exception("Intentional!");
            };

            ManualResetEvent mre = new (false);
            var q = new WorkQueue<ManualResetEvent>(flipFlop, _noTimerOptions);

            q.OnCompleted += MreOnCompleted;

            var wrapper = q.Enqueue(mre);

            wrapper.Id.Should().NotBe(default(Guid));

            wrapper.Attempts.Should().NotBeEmpty();

            q.DoWork();

            mre.WaitOne();

            q.Succeeded.Any(w => w.Id == wrapper.Id).Should().BeTrue();
        }

        [TestMethod()]
        public void CancelTest()
        {
            int counter = 0;

            Action<ManualResetEvent> flipFlop = mre => {
                if(counter++ % 2 == 1) mre.Set(); 
                else throw new Exception("Intentional!");
            };

            ManualResetEvent mre = new (false);
            var q = new WorkQueue<ManualResetEvent>(flipFlop, _noTimerOptions);

            q.OnCompleted += MreOnCompleted;

            var wrapper = q.Enqueue(mre);

            wrapper.Id.Should().NotBe(default(Guid));

            wrapper.Attempts.Should().NotBeEmpty();
            
            q.Cancel(wrapper.Id).Should().BeTrue();

            wrapper.IsCanceled.Should().BeTrue();

            q.DoWork();

            q.Succeeded.Should().BeEmpty();
        }
    }

    internal struct ResettablePair
    {
        public int Value;
        public ManualResetEvent Mre;

        public ResettablePair(int value, ManualResetEvent mre)
        {
            Value = value;
            Mre = mre;
        }

        public override bool Equals(object obj)
        {
            return obj is ResettablePair other &&
                   Value == other.Value &&
                   EqualityComparer<ManualResetEvent>.Default.Equals(Mre, other.Mre);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value, Mre);
        }

        public void Deconstruct(out int v, out ManualResetEvent item2)
        {
            v = this.Value;
            item2 = Mre;
        }

        public static implicit operator (int v, ManualResetEvent)(ResettablePair value)
        {
            return (value.Value, value.Mre);
        }

        public static implicit operator ResettablePair((int v, ManualResetEvent) value)
        {
            return new ResettablePair(value.v, value.Item2);
        }
    }
}