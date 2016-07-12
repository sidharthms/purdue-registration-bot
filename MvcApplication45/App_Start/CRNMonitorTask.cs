using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Entity;
using MvcApplication45.Common;
using MvcApplication45.DAL;
using MvcApplication45.Models;

namespace MvcApplication45 {
    public class CRNMonitorTask {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// CRN being monitored.
        /// </summary>
        public int CRN { get; set; }

        public int TermId { get; set; }

        public int CheckInterval { get; set; }

        public Task<bool> MonitorTask { get; set; }

        /// <summary>
        /// Cancellation token source for task.
        /// </summary>
        public CancellationTokenSource TokenSource { get; set; }

        /// <summary>
        /// Check if the CRN has been marked available. Note: Class may not 
        /// actually be available at the time of checking.
        /// </summary>
        private bool _Available;
        private object _AvailableLock = new object();

        public bool Available {
            get { 
                lock (_AvailableLock) { 
                    return _Available; 
                } 
            }
            set {
                lock (_AvailableLock) {
                    _Available = value;
                }
            }
        }
        

        public bool Failed { get; set; }

        public CRNMonitorTask(Models.CRNToAdd crnInfo) {
            CRN = crnInfo.Number;
            TermId = crnInfo.RegTasks[0].Group.TermId;
            CheckInterval = crnInfo.RegTasks.Min(t => t.CheckInterval);
            MonitorTask = null;
            Available = false;
            Failed = false;
        }

        private Object _MonitorStartLock = new Object();
        public void StartMonitoringIfNot() {
            lock (_MonitorStartLock) {
                if (MonitorTask == null || MonitorTask.IsCompleted) {
                    Available = false;
                    Failed = false;
                    TokenSource = new CancellationTokenSource();

                    var token = TokenSource.Token;

                    MonitorTask = Task.Run<bool>(async () => {
                        return await RegistrationUtility.MonitorCRN(CRN,
                            TermId, CheckInterval, token);
                    }, token);

                    MonitorTask.ContinueWith(t => {
                        RegisterIfSuccessful(t.Result, token);
                    }, token);
                }
            }
        }

        private void RegisterIfSuccessful(bool success, CancellationToken ct) {
            if (!success) {
                Failed = true;
                ++Program.FailedCount;
                return;
            }
            Available = true;

            // Check if tasks assocaited with this CRN can be registered.
            using (var db = new DataContext()) {
                var crnInfo = db.CRNsToAdd.Include(c => c.RegTasks).Single(c => c.Number == CRN);

                // Make list to tasks associated with CRN.
                var tasksToRegister = new List<RegTask>();
                foreach (var regTask in crnInfo.RegTasks) {
                    var allAvailable = true;

                    // Check if other CRNs in task are also available.
                    foreach (var crn in regTask.CRNsToAdd) {
                        CRNMonitorTask monitorTask;
                        if (QueueUtility.MonitoringTasks.TryGetValue(crn.Number, out monitorTask))
                            if (!ct.IsCancellationRequested && monitorTask.Available)
                                continue;
                        allAvailable = false;
                        break;
                    }
                    if (allAvailable)
                        tasksToRegister.Add(regTask);
                }

                // Sort tasks according to Priority.
                var sortedTasks = tasksToRegister.OrderBy(t => t.Priority).ToList();
                if (!ct.IsCancellationRequested) {
                    sortedTasks.ForEach(t => QueueUtility.TasksToRegister.Enqueue(t.Id));
                    QueueUtility.RegisterRequest.Set();
                }
            }
        }
    }
}
