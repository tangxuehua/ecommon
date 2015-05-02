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
            var getContextInfo = new Func<string>(() =>
            {
                try
                {
                    return getContextInfoFunc();
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to execute the getContextInfoFunc.", ex);
                    return null;
                }
            });
            var retryAction = new Action<int>(currentRetryTimes =>
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
                    _logger.Error("Failed to execute the retryAction.", ex);
                }
            });
            var executeFailedAction = new Action<string>(errorMessage =>
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
                    _logger.Error(string.Format("Failed to execute the failedCallbackAction of asyncAction:{0}, contextInfo:{1}", asyncActionName, getContextInfo()), ex);
                }
            });
            var processTaskException = new Action<Exception, int>((ex, currentRetryTimes) =>
            {
                if (ex is IOException)
                {
                    _logger.Error(string.Format("Async task '{0}' has io exception, contextInfo:{1}, current retryTimes:{2}, try to run the async task again.", asyncActionName, getContextInfo(), currentRetryTimes), ex);
                    retryAction(retryTimes);
                }
                else
                {
                    _logger.Error(string.Format("Async task '{0}' has unknown exception, contextInfo:{1}, current retryTimes:{2}", asyncActionName, getContextInfo(), currentRetryTimes), ex);
                    if (retryWhenFailed)
                    {
                        retryAction(retryTimes);
                    }
                    else
                    {
                        executeFailedAction(ex.Message);
                    }
                }
            });
            var completeAction = new Action<Task<TAsyncResult>>(t =>
            {
                try
                {
                    if (t.Exception != null)
                    {
                        processTaskException(t.Exception.InnerException, retryTimes);
                        return;
                    }
                    if (t.IsCanceled)
                    {
                        _logger.ErrorFormat("Async task '{0}' was cancelled, contextInfo:{1}, current retryTimes:{2}.", asyncActionName, getContextInfo(), retryTimes);
                        executeFailedAction(string.Format("Async task '{0}' was cancelled.", asyncActionName));
                        return;
                    }
                    var result = t.Result;
                    if (result == null)
                    {
                        _logger.ErrorFormat("Async task '{0}' result is null, contextInfo:{1}, current retryTimes:{2}", asyncActionName, getContextInfo(), retryTimes);
                        if (retryWhenFailed)
                        {
                            retryAction(retryTimes);
                        }
                        else
                        {
                            executeFailedAction(string.Format("Async task '{0}' result is null.", asyncActionName));
                        }
                        return;
                    }
                    if (result.Status == AsyncTaskStatus.Success)
                    {
                        if (successAction != null)
                        {
                            successAction(result);
                        }
                    }
                    else if (result.Status == AsyncTaskStatus.IOException)
                    {
                        _logger.ErrorFormat("Async task '{0}' result status is io exception, contextInfo:{1}, current retryTimes:{2}, errorMsg:{3}, try to run the async task again.", asyncActionName, getContextInfo(), retryTimes, result.ErrorMessage);
                        retryAction(retryTimes);
                    }
                    else if (result.Status == AsyncTaskStatus.Failed)
                    {
                        _logger.ErrorFormat("Async task '{0}' failed, contextInfo:{1}, current retryTimes:{2}, errorMsg:{3}", asyncActionName, getContextInfo(), retryTimes, result.ErrorMessage);
                        if (retryWhenFailed)
                        {
                            retryAction(retryTimes);
                        }
                        else
                        {
                            executeFailedAction(result.ErrorMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to execute the completeAction.", ex);
                }
            });

            try
            {
                asyncAction().ContinueWith(completeAction);
            }
            catch (IOException ex)
            {
                _logger.Error(string.Format("IOException raised when executing async task '{0}', contextInfo:{1}, current retryTimes:{2}, try to execute the async task again.", asyncActionName, getContextInfo(), retryTimes), ex);
                retryAction(retryTimes);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Unknown exception raised when executing async task '{0}', contextInfo:{1}, current retryTimes:{2}", asyncActionName, getContextInfo(), retryTimes), ex);
                if (retryWhenFailed)
                {
                    retryAction(retryTimes);
                }
                else
                {
                    executeFailedAction(ex.Message);
                }
            }
        }
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
