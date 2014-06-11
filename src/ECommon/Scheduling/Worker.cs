using System;
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
        private readonly string _actionName;
        private readonly Action _action;
        private readonly ILogger _logger;
        private ThreadTask _currentTask;

        /// <summary>Returns the loop action name of the current worker.
        /// </summary>
        public string ActionName
        {
            get { return _actionName; }
        }
        /// <summary>Return the IsAlive status of the current worker thread.
        /// </summary>
        public bool IsAlive
        {
            get
            {
                return _currentTask != null && _currentTask.Thread != null && _currentTask.Thread.IsAlive;
            }
        }

        /// <summary>Initialize a new Worker thread with the specified action.
        /// </summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="action">The action to run in a loop.</param>
        public Worker(string actionName, Action action)
        {
            _actionName = actionName;
            _action = action;
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        /// <summary>Start the worker.
        /// </summary>
        public Worker Start()
        {
            var thread = new Thread(Loop) { IsBackground = true };
            var task = new ThreadTask { Thread = thread, RequestingStop = false };

            thread.Name = string.Format("{0}.Worker", _actionName);
            thread.Start(task);

            _currentTask = task;

            _logger.DebugFormat("Worker started, actionName:{0}, managedThreadId:{1}, nativeThreadId:{2}", _actionName, thread.ManagedThreadId, GetNativeThreadId(thread));

            return this;
        }
        /// <summary>Stop the worker.
        /// </summary>
        public Worker Stop()
        {
            _currentTask.RequestingStop = true;
            _logger.DebugFormat("Worker stop requested, actionName:{0}, managedThreadId:{1}, nativeThreadId:{2}", _actionName, _currentTask.Thread.ManagedThreadId, GetNativeThreadId(_currentTask.Thread));
            return this;
        }

        private void Loop(object parameter)
        {
            var task = (ThreadTask)parameter;
            while (!task.RequestingStop)
            {
                try
                {
                    _action();
                }
                catch (ThreadAbortException abortException)
                {
                    _logger.Error("caught ThreadAbortException - resetting.", abortException);
                    Thread.ResetAbort();
                    _logger.Info("ThreadAbortException resetted.");
                }
                catch (Exception ex)
                {
                    _logger.Error("Worker action has exception.", ex);
                }
            }
            _logger.DebugFormat("Worker exited, actionName:{0}, managedThreadId:{1}, nativeThreadId:{2}", _actionName, task.Thread.ManagedThreadId, GetNativeThreadId(task.Thread));
        }
        private static int GetNativeThreadId(Thread thread)
        {
            var f = typeof(Thread).GetField("DONT_USE_InternalThread", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var pInternalThread = (IntPtr)f.GetValue(thread);
            var nativeId = Marshal.ReadInt32(pInternalThread, (IntPtr.Size == 8) ? 548 : 348); // found by analyzing the memory
            return nativeId;
        }

        class ThreadTask
        {
            public Thread Thread;
            public volatile bool RequestingStop;
        }
    }
}
