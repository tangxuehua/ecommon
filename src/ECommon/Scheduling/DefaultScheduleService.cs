using System;
using System.Collections.Concurrent;
using System.Threading;
using ECommon.Logging;

namespace ECommon.Scheduling
{
    public class DefaultScheduleService : IScheduleService
    {
        private readonly ConcurrentDictionary<int, TimerBasedTask> _taskDict = new ConcurrentDictionary<int, TimerBasedTask>();
        private readonly ILogger _logger;
        private int _maxTaskId;

        public DefaultScheduleService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create(GetType().FullName);
        }

        public int ScheduleTask(string actionName, Action action, int dueTime, int period)
        {
            var newTaskId = Interlocked.Increment(ref _maxTaskId);
            var timer = new Timer((obj) =>
            {
                var currentTaskId = (int)obj;
                TimerBasedTask currentTask;
                if (_taskDict.TryGetValue(currentTaskId, out currentTask))
                {
                    if (currentTask.Stoped)
                    {
                        return;
                    }

                    try
                    {
                        currentTask.Timer.Change(Timeout.Infinite, Timeout.Infinite);
                        if (currentTask.Stoped)
                        {
                            return;
                        }
                        currentTask.Action();
                    }
                    catch (ObjectDisposedException) { }
                    catch (Exception ex)
                    {
                        _logger.Error(string.Format("Task has exception, actionName:{0}, dueTime:{1}, period:{2}", currentTask.ActionName, currentTask.DueTime, currentTask.Period), ex);
                    }
                    finally
                    {
                        if (!currentTask.Stoped)
                        {
                            try
                            {
                                currentTask.Timer.Change(currentTask.Period, currentTask.Period);
                            }
                            catch (ObjectDisposedException) { }
                        }
                    }
                }
            }, newTaskId, Timeout.Infinite, Timeout.Infinite);

            if (!_taskDict.TryAdd(newTaskId, new TimerBasedTask { ActionName = actionName, Action = action, Timer = timer, DueTime = dueTime, Period = period, Stoped = false }))
            {
                _logger.ErrorFormat("Schedule task failed, actionName:{0}, dueTime:{1}, period:{2}", actionName, dueTime, period);
                return -1;
            }

            timer.Change(dueTime, period);
            _logger.DebugFormat("Schedule task success, actionName:{0}, dueTime:{1}, period:{2}", actionName, dueTime, period);

            return newTaskId;
        }
        public void ShutdownTask(int taskId)
        {
            TimerBasedTask task;
            if (_taskDict.TryRemove(taskId, out task))
            {
                task.Stoped = true;
                task.Timer.Change(Timeout.Infinite, Timeout.Infinite);
                task.Timer.Dispose();
                _logger.DebugFormat("Shutdown task success, actionName:{0}, dueTime:{1}, period:{2}", task.ActionName, task.DueTime, task.Period);
            }
        }

        class TimerBasedTask
        {
            public string ActionName;
            public Action Action;
            public Timer Timer;
            public int DueTime;
            public int Period;
            public bool Stoped;
        }
    }
}
