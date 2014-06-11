using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
        private readonly string _actionName;
        private readonly Action _action;
        private Thread _thread;
        private ILogger _logger;

        /// <summary>Returns the loop action name of the current worker.
        /// </summary>
        public string ActionName
        {
            get { return _actionName; }
        }
        /// <summary>Returns the thread of the current worker.
        /// </summary>
        public Thread Thread
        {
            get { return _thread; } 
        }
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
        /// <param name="actionName">The delegate action name.</param>
        /// <param name="action">The delegate action to run in a loop.</param>
        public Worker(string actionName, Action action) : this(actionName, action, 0) { }
        /// <summary>Initialize a new Worker thread with the specified action.
        /// </summary>
        /// <param name="actionName">The delegate action name.</param>
        /// <param name="action">The delegate action to run in a loop.</param>
        /// <param name="intervalMilliseconds">The action run interval milliseconds.</param>
        public Worker(string actionName, Action action, int intervalMilliseconds)
        {
            _actionName = actionName;
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
            _logger.InfoFormat("Worker thread started, nativeThreadId:{0}, loop action:{1}", GetNativeThreadId(_thread), _actionName);
            return this;
        }
        /// <summary>Stop the worker.
        /// </summary>
        public Worker Stop()
        {
            _requestingStop = true;
            _logger.InfoFormat("Worker thread requesting stop, loop action:{0}", _actionName);
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

        private static int GetNativeThreadId(Thread thread)
        {
            var f = typeof(Thread).GetField("DONT_USE_InternalThread", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var pInternalThread = (IntPtr)f.GetValue(thread);
            var nativeId = Marshal.ReadInt32(pInternalThread, (IntPtr.Size == 8) ? 548 : 348); // found by analyzing the memory
            return nativeId;
        }
    }
}
