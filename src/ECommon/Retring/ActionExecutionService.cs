using System;
using System.Collections.Concurrent;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.Scheduling;

namespace ECommon.Retring
{
    /// <summary>The default implementation of IActionExecutionService.
    /// </summary>
    public class ActionExecutionService : IActionExecutionService
    {
        private readonly int _retryActionDefaultPeriod;
        private readonly BlockingCollection<ActionInfo> _retryQueue;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger _logger;

        /// <summary>Parameterized constructor.
        /// </summary>
        /// <param name="loggerFactory"></param>
        public ActionExecutionService(ILoggerFactory loggerFactory)
        {
            _retryActionDefaultPeriod = Configuration.Instance.Setting.RetryActionDefaultPeriod;
            _retryQueue = new BlockingCollection<ActionInfo>(new ConcurrentQueue<ActionInfo>());
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
            _logger = loggerFactory.Create(GetType().FullName);
            _scheduleService.ScheduleTask("ActionExecutionService.RetryAction", RetryAction, _retryActionDefaultPeriod, _retryActionDefaultPeriod);
        }

        /// <summary>Try to execute the given action with the given max retry count.
        /// <remarks>If the action execute still failed when reached to the max retry count, then put the action into the retry queue.
        /// </remarks>
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="action"></param>
        /// <param name="maxRetryCount"></param>
        /// <param name="nextAction"></param>
        public void TryAction(string actionName, Func<bool> action, int maxRetryCount, ActionInfo nextAction)
        {
            if (TryWithMaxCount(actionName, (x, y, z) => action(), 0, maxRetryCount))
            {
                ExecuteAction(nextAction);
            }
            else
            {
                _retryQueue.Add(new ActionInfo(actionName, obj => action(), null, nextAction));
            }
        }
        /// <summary>Try to execute the given action with the given max retry count. If success then returns true; otherwise, returns false.
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="action"></param>
        /// <param name="maxRetryCount"></param>
        /// <returns></returns>
        public bool TryRecursively(string actionName, Func<bool> action, int maxRetryCount)
        {
            return TryWithMaxCount(actionName, (x, y, z) => action(), 0, maxRetryCount);
        }

        private bool TryWithMaxCount(string actionName, Func<string, int, int, bool> action, int retriedCount, int maxRetryCount)
        {
            var success = false;
            try
            {
                success = action(actionName, retriedCount, maxRetryCount);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Exception raised when executing action {0}.", actionName), ex);
            }

            if (success)
            {
                return true;
            }
            else if (retriedCount < maxRetryCount)
            {
                return TryWithMaxCount(actionName, action, retriedCount + 1, maxRetryCount);
            }
            return false;
        }
        private void RetryAction()
        {
            var actionInfo = _retryQueue.Take();
            try
            {
                ExecuteAction(actionInfo);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("RetryAction has exception, actionName:{0}", actionInfo.Name), ex);
            }
        }
        private void ExecuteAction(ActionInfo actionInfo)
        {
            if (actionInfo == null) return;

            var success = false;

            try
            {
                success = actionInfo.Action(actionInfo.Data);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Exception raised when executing action {0}.", actionInfo.Name), ex);
            }
            finally
            {
                if (success)
                {
                    if (actionInfo.Next != null)
                    {
                        ExecuteAction(actionInfo.Next);
                    }
                }
                else
                {
                    _retryQueue.Add(actionInfo);
                }
            }
        }
    }
}
