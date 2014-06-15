using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Utilities;

namespace ECommon.Scheduling
{
    /// <summary>Represent a background worker that will repeatedly execute a specific method.
    /// </summary>
    public class Worker
    {
        private readonly string _actionName;
        private readonly Action _action;
        private readonly ILogger _logger;
        private WorkerState _currentState;

        /// <summary>Returns the action name of the current worker.
        /// </summary>
        public string ActionName
        {
            get { return _actionName; }
        }

        /// <summary>Initialize a new worker with the specified action.
        /// </summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="action">The action to run by the worker.</param>
        public Worker(string actionName, Action action)
        {
            _actionName = actionName;
            _action = action;
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        /// <summary>Start the worker if it is not running.
        /// </summary>
        public Worker Start()
        {
            if (_currentState != null && !_currentState.StopRequested) return this;

            var thread = new Thread(Loop)
            {
                Name = string.Format("{0}.Worker", _actionName),
                IsBackground = true
            };
            var state = new WorkerState();

            thread.Start(state);

            _currentState = state;
            _logger.DebugFormat("Worker started, actionName:{0}, id:{1}, managedThreadId:{2}, nativeThreadId:{3}", _actionName, state.Id, thread.ManagedThreadId, GetNativeThreadId(thread));

            return this;
        }
        /// <summary>Request to stop the worker.
        /// </summary>
        public Worker Stop()
        {
            _currentState.StopRequested = true;
            _logger.DebugFormat("Worker stop requested, actionName:{0}, id:{1}", _actionName, _currentState.Id);
            return this;
        }

        private void Loop(object data)
        {
            var state = (WorkerState)data;

            while (!state.StopRequested)
            {
                try
                {
                    _action();
                }
                catch (ThreadAbortException)
                {
                    _logger.InfoFormat("caught ThreadAbortException, try to resetting, actionName:{0}", _actionName);
                    Thread.ResetAbort();
                    _logger.InfoFormat("ThreadAbortException resetted, actionName:{0}", _actionName);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Worker action has exception, actionName:{0}", _actionName), ex);
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

        class WorkerState
        {
            public string Id = ObjectId.GenerateNewStringId();
            public bool StopRequested;
        }
    }
}
