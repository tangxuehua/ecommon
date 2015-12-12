using System;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Extensions;
using ECommon.Logging;
using ECommon.Utilities;

namespace ECommon.IO
{
    /// <summary>An IO action helper class.
    /// </summary>
    public class IOHelper
    {
        private readonly ILogger _logger;

        public IOHelper(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create(GetType().FullName);
        }

        public void TryIOAction(string actionName, Func<string> getContextInfo, Action action, int maxRetryTimes, bool continueRetryWhenRetryFailed = false, int retryInterval = 1000)
        {
            Ensure.NotNull(actionName, "actionName");
            Ensure.NotNull(getContextInfo, "getContextInfo");
            Ensure.NotNull(action, "action");
            TryIOActionRecursivelyInternal(actionName, getContextInfo, (x, y, z) => action(), 0, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
        }
        public T TryIOFunc<T>(string funcName, Func<string> getContextInfo, Func<T> func, int maxRetryTimes, bool continueRetryWhenRetryFailed = false, int retryInterval = 1000)
        {
            Ensure.NotNull(funcName, "funcName");
            Ensure.NotNull(getContextInfo, "getContextInfo");
            Ensure.NotNull(func, "func");
            return TryIOFuncRecursivelyInternal(funcName, getContextInfo, (x, y, z) => func(), 0, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
        }

        #region TryAsyncActionRecursively

        public void TryAsyncActionRecursively<TAsyncResult>(
            string asyncActionName,
            Func<Task<TAsyncResult>> asyncAction,
            Action<int> mainAction,
            Action<TAsyncResult> successAction,
            Func<string> getContextInfoFunc,
            Action<string> failedAction,
            int retryTimes,
            bool retryWhenFailed = false,
            int maxRetryTimes = 3,
            int retryInterval = 1000) where TAsyncResult : AsyncTaskResult
        {
            try
            {
                asyncAction().ContinueWith(TaskContinueAction, new TaskExecutionContext<TAsyncResult>
                {
                    AsyncActionName = asyncActionName,
                    MainAction = mainAction,
                    SuccessAction = successAction,
                    GetContextInfoFunc = getContextInfoFunc,
                    FailedAction = failedAction,
                    RetryTimes = retryTimes,
                    RetryWhenFailed = retryWhenFailed,
                    MaxRetryTimes = maxRetryTimes,
                    RetryInterval = retryInterval
                });
            }
            catch (IOException ex)
            {
                _logger.Error(string.Format("IOException raised when executing async task '{0}', contextInfo:{1}, current retryTimes:{2}, try to execute the async task again.", asyncActionName, GetContextInfo(getContextInfoFunc), retryTimes), ex);
                ExecuteRetryAction(asyncActionName, getContextInfoFunc, mainAction, retryTimes, maxRetryTimes, retryInterval);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Unknown exception raised when executing async task '{0}', contextInfo:{1}, current retryTimes:{2}", asyncActionName, GetContextInfo(getContextInfoFunc), retryTimes), ex);
                if (retryWhenFailed)
                {
                    ExecuteRetryAction(asyncActionName, getContextInfoFunc, mainAction, retryTimes, maxRetryTimes, retryInterval);
                }
                else
                {
                    ExecuteFailedAction(asyncActionName, getContextInfoFunc, failedAction, ex.Message);
                }
            }
        }

        private string GetContextInfo(Func<string> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to execute the getContextInfoFunc.", ex);
                return null;
            }
        }
        private void ExecuteFailedAction(string asyncActionName, Func<string> getContextInfoFunc, Action<string> failedAction, string errorMessage)
        {
            try
            {
                if (failedAction != null)
                {
                    failedAction(errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Failed to execute the failedAction of asyncAction:{0}, contextInfo:{1}", asyncActionName, GetContextInfo(getContextInfoFunc)), ex);
            }
        }
        private void ExecuteRetryAction(string asyncActionName, Func<string> getContextInfoFunc, Action<int> mainAction, int currentRetryTimes, int maxRetryTimes, int retryInterval)
        {
            try
            {
                if (currentRetryTimes >= maxRetryTimes)
                {
                    Task.Factory.StartDelayedTask(retryInterval, () => mainAction(currentRetryTimes + 1));
                }
                else
                {
                    mainAction(currentRetryTimes + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Failed to execute the retryAction, asyncActionName:{0}, contextInfo:{1}", asyncActionName, GetContextInfo(getContextInfoFunc)), ex);
            }
        }
        private void ProcessTaskException(string asyncActionName, Func<string> getContextInfoFunc, Action<int> mainAction, Action<string> failedAction, Exception exception, int currentRetryTimes, int maxRetryTimes, int retryInterval, bool retryWhenFailed)
        {
            if (exception is IOException)
            {
                _logger.Error(string.Format("Async task '{0}' has io exception, contextInfo:{1}, current retryTimes:{2}, try to run the async task again.", asyncActionName, GetContextInfo(getContextInfoFunc), currentRetryTimes), exception);
                ExecuteRetryAction(asyncActionName, getContextInfoFunc, mainAction, currentRetryTimes, maxRetryTimes, retryInterval);
            }
            else
            {
                _logger.Error(string.Format("Async task '{0}' has unknown exception, contextInfo:{1}, current retryTimes:{2}", asyncActionName, GetContextInfo(getContextInfoFunc), currentRetryTimes), exception);
                if (retryWhenFailed)
                {
                    ExecuteRetryAction(asyncActionName, getContextInfoFunc, mainAction, currentRetryTimes, maxRetryTimes, retryInterval);
                }
                else
                {
                    ExecuteFailedAction(asyncActionName, getContextInfoFunc, failedAction, exception.Message);
                }
            }
        }
        private void TaskContinueAction<TAsyncResult>(Task<TAsyncResult> task, object obj) where TAsyncResult : AsyncTaskResult
        {
            var context = obj as TaskExecutionContext<TAsyncResult>;
            try
            {
                if (task.Exception != null)
                {
                    ProcessTaskException(
                        context.AsyncActionName,
                        context.GetContextInfoFunc,
                        context.MainAction,
                        context.FailedAction,
                        task.Exception,
                        context.RetryTimes,
                        context.MaxRetryTimes,
                        context.RetryInterval,
                        context.RetryWhenFailed);
                    return;
                }
                if (task.IsCanceled)
                {
                    _logger.ErrorFormat("Async task '{0}' was cancelled, contextInfo:{1}, current retryTimes:{2}.",
                        context.AsyncActionName,
                        GetContextInfo(context.GetContextInfoFunc),
                        context.RetryTimes);
                    ExecuteFailedAction(
                        context.AsyncActionName,
                        context.GetContextInfoFunc,
                        context.FailedAction,
                        string.Format("Async task '{0}' was cancelled.", context.AsyncActionName));
                    return;
                }
                var result = task.Result;
                if (result == null)
                {
                    _logger.ErrorFormat("Async task '{0}' result is null, contextInfo:{1}, current retryTimes:{2}",
                        context.AsyncActionName,
                        GetContextInfo(context.GetContextInfoFunc),
                        context.RetryTimes);
                    if (context.RetryWhenFailed)
                    {
                        ExecuteRetryAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.MainAction,
                            context.RetryTimes,
                            context.MaxRetryTimes,
                            context.RetryInterval);
                    }
                    else
                    {
                        ExecuteFailedAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.FailedAction,
                            string.Format("Async task '{0}' result is null.", context.AsyncActionName));
                    }
                    return;
                }
                if (result.Status == AsyncTaskStatus.Success)
                {
                    if (context.SuccessAction != null)
                    {
                        context.SuccessAction(result);
                    }
                }
                else if (result.Status == AsyncTaskStatus.IOException)
                {
                    _logger.ErrorFormat("Async task '{0}' result status is io exception, contextInfo:{1}, current retryTimes:{2}, errorMsg:{3}, try to run the async task again.",
                        context.AsyncActionName,
                        GetContextInfo(context.GetContextInfoFunc),
                        context.RetryTimes,
                        result.ErrorMessage);
                    ExecuteRetryAction(
                        context.AsyncActionName,
                        context.GetContextInfoFunc,
                        context.MainAction,
                        context.RetryTimes,
                        context.MaxRetryTimes,
                        context.RetryInterval);
                }
                else if (result.Status == AsyncTaskStatus.Failed)
                {
                    _logger.ErrorFormat("Async task '{0}' failed, contextInfo:{1}, current retryTimes:{2}, errorMsg:{3}",
                        context.AsyncActionName,
                        GetContextInfo(context.GetContextInfoFunc),
                        context.RetryTimes,
                        result.ErrorMessage);
                    if (context.RetryWhenFailed)
                    {
                        ExecuteRetryAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.MainAction,
                            context.RetryTimes,
                            context.MaxRetryTimes,
                            context.RetryInterval);
                    }
                    else
                    {
                        ExecuteFailedAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.FailedAction,
                            result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Failed to execute the taskContinueAction, asyncActionName:{0}, contextInfo:{1}",
                    context.AsyncActionName,
                    GetContextInfo(context.GetContextInfoFunc)), ex);
            }
        }

        class TaskExecutionContext<TAsyncResult>
        {
            public string AsyncActionName;
            public Action<int> MainAction;
            public Action<TAsyncResult> SuccessAction;
            public Func<string> GetContextInfoFunc;
            public Action<string> FailedAction;
            public int RetryTimes;
            public bool RetryWhenFailed;
            public int MaxRetryTimes;
            public int RetryInterval;
        }

        #endregion

        public void TryIOAction(Action action, string actionName)
        {
            Ensure.NotNull(action, "action");
            Ensure.NotNull(actionName, "actionName");
            try
            {
                action();
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format("{0} failed.", actionName), ex);
            }
        }
        public Task TryIOActionAsync(Func<Task> action, string actionName)
        {
            Ensure.NotNull(action, "action");
            Ensure.NotNull(actionName, "actionName");
            try
            {
                return action();
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format("{0} failed.", actionName), ex);
            }
        }
        public T TryIOFunc<T>(Func<T> func, string funcName)
        {
            Ensure.NotNull(func, "func");
            Ensure.NotNull(funcName, "funcName");
            try
            {
                return func();
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format("{0} failed.", funcName), ex);
            }
        }
        public Task<T> TryIOFuncAsync<T>(Func<Task<T>> func, string funcName)
        {
            Ensure.NotNull(func, "func");
            Ensure.NotNull(funcName, "funcName");
            try
            {
                return func();
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format("{0} failed.", funcName), ex);
            }
        }

        private void TryIOActionRecursivelyInternal(string actionName, Func<string> getContextInfo, Action<string, Func<string>, int> action, int retryTimes, int maxRetryTimes, bool continueRetryWhenRetryFailed = false, int retryInterval = 1000)
        {
            try
            {
                action(actionName, getContextInfo, retryTimes);
            }
            catch (IOException ex)
            {
                var errorMessage = string.Format("IOException raised when executing action '{0}', currentRetryTimes:{1}, maxRetryTimes:{2}, contextInfo:{3}", actionName, retryTimes, maxRetryTimes, getContextInfo());
                _logger.Error(errorMessage, ex);
                if (retryTimes >= maxRetryTimes)
                {
                    if (!continueRetryWhenRetryFailed)
                    {
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(retryInterval);
                    }
                }
                retryTimes++;
                TryIOActionRecursivelyInternal(actionName, getContextInfo, action, retryTimes, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("Unknown exception raised when executing action '{0}', currentRetryTimes:{1}, maxRetryTimes:{2}, contextInfo:{3}", actionName, retryTimes, maxRetryTimes, getContextInfo());
                _logger.Error(errorMessage, ex);
                throw;
            }
        }
        private T TryIOFuncRecursivelyInternal<T>(string funcName, Func<string> getContextInfo, Func<string, Func<string>, long, T> func, int retryTimes, int maxRetryTimes, bool continueRetryWhenRetryFailed = false, int retryInterval = 1000)
        {
            try
            {
                return func(funcName, getContextInfo, retryTimes);
            }
            catch (IOException ex)
            {
                var errorMessage = string.Format("IOException raised when executing func '{0}', currentRetryTimes:{1}, maxRetryTimes:{2}, contextInfo:{3}", funcName, retryTimes, maxRetryTimes, getContextInfo());
                _logger.Error(errorMessage, ex);
                if (retryTimes >= maxRetryTimes)
                {
                    if (!continueRetryWhenRetryFailed)
                    {
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(retryInterval);
                    }
                }
                retryTimes++;
                return TryIOFuncRecursivelyInternal(funcName, getContextInfo, func, retryTimes, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("Unknown exception raised when executing func '{0}', currentRetryTimes:{1}, maxRetryTimes:{2}, contextInfo:{3}", funcName, retryTimes, maxRetryTimes, getContextInfo());
                _logger.Error(errorMessage, ex);
                throw;
            }
        }
    }
}
