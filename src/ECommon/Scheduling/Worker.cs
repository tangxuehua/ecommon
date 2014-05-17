using System;
using System.Threading;
using ECommon.Components;
using ECommon.Logging;

namespace ECommon.Scheduling
{
    /// <summary>Represent a background worker that will repeatedly execute a specific method.
    /// </summary>
    public class Worker
    {
        private volatile bool _requestingStop;
        private readonly Action _action;
        private Thread _thread;
        private ILogger _logger;

        /// <summary>Return the IsAlive status of the current worker thread.
        /// </summary>
        public bool IsAlive
        {
            get
            {
                return _thread != null && _thread.IsAlive;
            }
        }
        /// <summary>Gets or sets the interval which the action executed.
        /// </summary>
        public int IntervalMilliseconds { get; set; }

        /// <summary>Initialize a new Worker thread with the specified action.
        /// </summary>
        /// <param name="action">The delegate action to run in a loop.</param>
        public Worker(Action action) : this(action, 0) { }
        /// <summary>Initialize a new Worker thread with the specified action.
        /// </summary>
        /// <param name="action">The delegate action to run in a loop.</param>
        /// <param name="intervalMilliseconds">The action run interval milliseconds.</param>
        public Worker(Action action, int intervalMilliseconds)
        {
            _action = action;
            IntervalMilliseconds = intervalMilliseconds;
        }

        /// <summary>Start the worker.
        /// </summary>
        public Worker Start()
        {
            _requestingStop = false;
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Name = string.Format("Worker Thread {0}", _thread.ManagedThreadId);
            _thread.Start();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName + "-" + _thread.ManagedThreadId);

            return this;
        }
        /// <summary>Stop the worker.
        /// </summary>
        public Worker Stop()
        {
            _requestingStop = true;
            return this;
        }

        /// <summary>Executes the delegate action until the <see cref="Stop"/> method is called.
        /// </summary>
        private void Loop()
        {
            while (!_requestingStop)
            {
                try
                {
                    _action();
                    if (IntervalMilliseconds > 0)
                    {
                        Thread.Sleep(IntervalMilliseconds);
                    }
                }
                catch (ThreadAbortException abortException)
                {
                    _logger.Error("caught ThreadAbortException - resetting.", abortException);
                    Thread.ResetAbort();
                    _logger.Info("ThreadAbortException resetted.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }
        }
    }
}
