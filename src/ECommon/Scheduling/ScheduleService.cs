using System;
using System.Collections.Generic;
using System.Threading;
using ECommon.Logging;

namespace ECommon.Scheduling
{
    public class ScheduleService : IScheduleService
    {
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, TimerBasedTask> _taskDict = new Dictionary<string, TimerBasedTask>();
        private readonly ILogger _logger;

        public ScheduleService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create(GetType().FullName);
        }

        public void StartTask(string name, Action action, int dueTime, int period)
        {
            lock (_lockObject)
            {
                if (_taskDict.ContainsKey(name)) return;
                var timer = new Timer(TaskCallback, name, Timeout.Infinite, Timeout.Infinite);
                _taskDict.Add(name, new TimerBasedTask { Name = name, Action = action, Timer = timer, DueTime = dueTime, Period = period });
                timer.Change(dueTime, period);
                _logger.InfoFormat("Task started, name:{0}, due:{1}, period:{2}", name, dueTime, period);
            }
        }
        public void StopTask(string name)
        {
            lock (_lockObject)
            {
                if (_taskDict.ContainsKey(name))
                {
                    _taskDict[name].Timer.Dispose();
                    _taskDict.Remove(name);
                    _logger.InfoFormat("Task stopped, name:{0}", name);
                }
            }
        }

        private void TaskCallback(object obj)
        {
            var taskName = (string)obj;
            TimerBasedTask task;

            if (_taskDict.TryGetValue(taskName, out task))
            {
                try
                {
                    task.Timer.Change(Timeout.Infinite, Timeout.Infinite);
                    task.Action();
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Task has exception, name:{0}, due:{1}, period:{2}", task.Name, task.DueTime, task.Period), ex);
                }
                finally
                {
                    try { task.Timer.Change(task.Period, task.Period); } catch { }
                }
            }
        }

        class TimerBasedTask
        {
            public string Name;
            public Action Action;
            public Timer Timer;
            public int DueTime;
            public int Period;
        }
    }
}
