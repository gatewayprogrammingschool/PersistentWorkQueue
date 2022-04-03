using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PersistentWorkQueue
{
    public class WorkQueue<TRequest> : IDisposable
    {
        private readonly ConcurrentQueue<RequestWrapper<TRequest>> _requestQueue
            = new ();

        private Timer? _timer;
        private bool disposedValue;

        public WorkQueue(Action<TRequest> action, WorkQueueOptions options)
        {
            QueueType = QueueTypes.Action;
            Action = action;
            Options = options;

            SetupWorkQueue();
        }

        public WorkQueue(Delegate @delegate, WorkQueueOptions options)
        {
            QueueType = QueueTypes.Delegate;
            Delegate = @delegate;
            Options = options;

            SetupWorkQueue();
        }

        public WorkQueue(QueueTypes queueType, WorkQueueOptions options)
        {
            QueueType = queueType;
            Options = options;

            SetupWorkQueue();
        }

        public WorkQueue(Action<TRequest> action)
        {
            QueueType = QueueTypes.Action;
            Action = action;
            Options = new();

            SetupWorkQueue();
        }

        public WorkQueue(Delegate @delegate)
        {
            QueueType = QueueTypes.Delegate;
            Delegate = @delegate;
            Options = new();

            SetupWorkQueue();
        }

        public WorkQueue(QueueTypes queueType)
        {
            QueueType = queueType;
            Options = new();

            SetupWorkQueue();
        }

        public ImmutableList<RequestWrapper<TRequest>> Succeeded
        {
            get;
            private set;
        } = new List<RequestWrapper<TRequest>>().ToImmutableList();

        public bool IsRunning => _timer is not null;


        private void SetupWorkQueue()
        {
            StartTimer();
        }

        public TimeSpan StartTimer()
        {
            if (IsRunning)
            {
                return Options.TimerInterval;
            }

            if (Options.TimerInterval != TimeSpan.Zero)
            {
                _timer = new Timer(
                    _ => DoWork(),
                    null,
                    Options.TimerInterval,
                    Options.TimerInterval);
            }

            return Options.TimerInterval;
        }

        public void StopTimer()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public IEnumerable<RequestWrapper<TRequest>> Enqueue(IEnumerable<TRequest> requests)
        {
            return Enqueue(requests, default);
        }

        public IEnumerable<RequestWrapper<TRequest>> Enqueue(IEnumerable<TRequest> requests, CancellationToken? token)
        {
            List<RequestWrapper<TRequest>> newItems = new();

            StopTimer();

            foreach (var request in requests)
            {
                RequestWrapper<TRequest> wrapper = new(request);

                _requestQueue.Enqueue(wrapper);

                OnPersist?.Invoke(wrapper);

                newItems.Add(wrapper);
            }

            DoWork(token);

            StartTimer();

            return newItems;
        }

        public RequestWrapper<TRequest> Enqueue(TRequest request)
        {
            return Enqueue(request, default);
        }

        public RequestWrapper<TRequest> Enqueue(TRequest request, CancellationToken? token)
        {
            RequestWrapper<TRequest> wrapper = new(request);

            _requestQueue.Enqueue(wrapper);

            OnPersist?.Invoke(wrapper);

            DoWork(token);

            return wrapper;
        }

        public event Action<TRequest>? OnAction;
        public event Action<RequestWrapper<TRequest>>? OnPersist;
        public event Action<RequestWrapper<TRequest>>? OnCompleted;

        public bool DoWork()
        {
            return DoWork(default);
        }

        public bool DoWork(CancellationToken? token)
        {
            var workList = _requestQueue.Where(w => !w.IsCanceled).ToList();
            try
            {
                _requestQueue.Clear();

                if (Options.FireAndForget)
                {
                    if(token is CancellationToken tk)
                    { 
                        Task.Run((Action)(() => PerformActions(workList)), tk);
                    }
                    else
                    {
                        Task.Run((Action)(() => PerformActions(workList)));
                    }
                }
                else
                {
                    return PerformActions(workList);
                }
            }
            finally
            {
                workList.ForEach(req => _requestQueue.Enqueue(req));
            }

            return false;
        }

        private bool PerformActions(List<RequestWrapper<TRequest>> workList)
        {
#pragma warning disable PH_S022 // Parallel.For with Monitor Synchronization
            var result = Parallel.ForEach(workList.ToList(), wrapper =>
            {
                try
                {
                    switch (QueueType)
                    {
                        case QueueTypes.Action:
                            Action?.Invoke(wrapper.Request);
                            break;

                        case QueueTypes.Delegate:
                            Delegate?.DynamicInvoke(wrapper.Request);
                            break;

                        case QueueTypes.Event:
                            OnAction?.Invoke(wrapper.Request);
                            break;
                    }

                    lock (_requestQueue)
                    { 
                        wrapper.CompletedOn = DateTimeOffset.UtcNow;
                        Succeeded = Succeeded.Add(wrapper);

                        workList.Remove(wrapper);
                    }

                    OnCompleted?.Invoke(wrapper);
                }
                catch (Exception ex)
                {
                    wrapper.Attempts.Add((DateTimeOffset.UtcNow, ex.ToString()));
                }
                finally
                {
                    OnPersist?.Invoke(wrapper);
                }
            });
#pragma warning restore PH_S022 // Parallel.For with Monitor Synchronization

            return result.IsCompleted;
        }

        public bool Cancel(Guid id)
        {
            var existing = _requestQueue.FirstOrDefault(w => w.Id == id);

            if (existing is not null)
            {
                return existing.IsCanceled = true;
            }

            return false;
        }

        public QueueTypes QueueType { get; }
        public Delegate? Delegate { get; }
        public Action<TRequest>? Action { get; }
        public WorkQueueOptions Options { get; }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                    _requestQueue.Clear();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
