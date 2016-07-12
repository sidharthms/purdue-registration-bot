using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Entity;
using MvcApplication45.Models;
using MvcApplication45.DAL;

namespace MvcApplication45.Common {
    public static class QueueUtility {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static Thread QueueingThread = null;
        public static readonly ConcurrentQueue<int> CRNsToRestart = new ConcurrentQueue<int>();
        public static readonly ConcurrentQueue<int> TasksToRegister = new ConcurrentQueue<int>();
        public static readonly ConcurrentDictionary<int, CRNMonitorTask> MonitoringTasks 
            = new ConcurrentDictionary<int, CRNMonitorTask>();

        public static readonly ManualResetEvent RestartRequest = new ManualResetEvent(false);
        public static readonly ManualResetEvent RefreshAllRequest = new ManualResetEvent(false);
        public static readonly ManualResetEvent TerminateRequest = new ManualResetEvent(false);
        public static readonly ManualResetEvent RegisterRequest = new ManualResetEvent(false);

        private static void RestartRequestedCRNs() {
            RestartRequest.Reset();
            while (!CRNsToRestart.IsEmpty) {
                int crn;
                if (CRNsToRestart.TryDequeue(out crn))
                    MonitoringTasks[crn].StartMonitoringIfNot();
            }
        }

        private static void RefreshRunningTasks() {
            RefreshAllRequest.Reset();
            var monitoredCRNs = MonitoringTasks.Keys;
            using (var db = new DataContext()) {
                // Get CRNs for incomplete tasks.
                var crns = db.RegTasks.Include(t => t.CRNsToAdd)
                    .Where(t => t.Status == RegTask.RegStatus.Incomplete)
                    .SelectMany(t => t.CRNsToAdd).Include(c => c.RegTasks.Select(t => t.Group)).Distinct().ToList();
                var crnsInDb = crns.Select(c => c.Number);

                // Get list of crns currently being monitored but have been removed from db.
                var invalidMonitorTasks = new List<CRNMonitorTask>();
                foreach (var crn in monitoredCRNs.Except(crnsInDb)) {
                    var invalidTask = MonitoringTasks[crn];
                    invalidTask.TokenSource.Cancel();
                    invalidMonitorTasks.Add(invalidTask);
                }
                Task.WaitAll(invalidMonitorTasks.Select(t => t.MonitorTask).ToArray());
                CRNMonitorTask unused;
                invalidMonitorTasks.ForEach(t => MonitoringTasks.TryRemove(t.CRN, out unused));

                monitoredCRNs = MonitoringTasks.Keys;

                // Find list of CRNs in db but are not being currently monitored.
                var toStart = new List<CRNMonitorTask>();
                foreach (var crn in crns) {
                    if (!monitoredCRNs.Contains(crn.Number))
                        toStart.Add(new CRNMonitorTask(crn));
                }

                // Start monitoring new tasks.
                toStart.ForEach(t => {
                    if (!TerminateRequest.WaitOne(0)) {
                        MonitoringTasks[t.CRN] = t;
                        t.StartMonitoringIfNot();
                    }
                });

                if (MonitoringTasks.Keys.Count == 0)
                    if (Program.FailedCount == 0) {
                        Program.AllDone.Set();
                    }
                        
            }
        }

        private static void Register() {
            RegisterRequest.Reset();

            var toRegister = new List<int>(TasksToRegister.Count);
            while (!TasksToRegister.IsEmpty) {
                int taskId;
                if (TasksToRegister.TryDequeue(out taskId))
                    toRegister.Add(taskId);
            }
            // Remove duplicates.
            toRegister = toRegister.Distinct().ToList();

            using (var db = new DataContext()) {
                foreach (var taskId in toRegister) {
                    var task = db.RegTasks.Include(t => t.Group.User).Include(t => t.CRNsToAdd)
                        .Include(t => t.CRNsToDelete).Single(t => t.Id == taskId);
                    if (!TerminateRequest.WaitOne(0)) {
                        foreach (var crn in task.CRNsToAdd)
                            crn.SpaceAvailable = RegistrationUtility.CheckCourseAvailable(
                                crn.Number, crn.RegTasks[0].Group.TermId) ?? 0;
                        var unavailableCRNs = task.CRNsToAdd.Where(crn => crn.SpaceAvailable <= 0).ToList();

                        if (unavailableCRNs.Any()) {
                            // Some CRNs does not seem to be available now :(
                            unavailableCRNs.ForEach(crn => QueueUtility.CRNsToRestart.Enqueue(crn.Number));
                            QueueUtility.RestartRequest.Set();
                        }
                        if (task.Status == RegTask.RegStatus.Incomplete) {
                            var status = RegistrationUtility.TryRegisterCourse(task).Result;
                            task.Status = status.Item1;
                            task.StatusDetails = status.Item2;
                            task.LastStatusChangeTime = DateTime.Now;
                            db.Entry(task).State = EntityState.Modified;
                            if (!task.NotifyOnly)
                                EmailUtility.SendStatusUpdateEmail(task.Group.User.Name,
                                    task.Group.User.Email, task.Course, 
                                    task.Status, task.StatusDetails);

                            if (task.Status == RegTask.RegStatus.Complete) {
                                foreach (var otherTask in task.Group.RegTasks
                                        .Where(t => t.Priority >= task.Priority && t.Id != task.Id)) {
                                    otherTask.Status = RegTask.RegStatus.AlternateComplete;
                                    otherTask.StatusDetails = "Task " + task.Id + " for course " 
                                        + task.Course + " registered instead";
                                    otherTask.LastStatusChangeTime = DateTime.Now;
                                    db.Entry(otherTask).State = EntityState.Modified;
                                }

                                foreach (var otherTask in task.Group.RegTasks
                                        .Where(t => t.Priority < task.Priority)) {
                                    // Deleted CRNs that otherTask didn't want to delete.
                                    var removedCrnsToAdd = task.CRNsToDelete.Except(otherTask.CRNsToDelete);

                                    // If crnToAdd already exists in db add that instance to list and don't create new.
                                    var crnsToAdd = new List<CRNToAdd>(removedCrnsToAdd.Count());
                                    foreach (var crnToAdd in removedCrnsToAdd) {
                                        var item = db.CRNsToAdd.Local.SingleOrDefault(crn => crn.Number == crnToAdd.Number);
                                        if (item == null) {
                                            item = new CRNToAdd() { Number = crnToAdd.Number };
                                            db.CRNsToAdd.Add(item);
                                        }
                                        crnsToAdd.Add(item);
                                    }
                                    otherTask.CRNsToAdd.AddRange(crnsToAdd);

                                    // If crnToDelete has already been deleted remove it from list.
                                    otherTask.CRNsToDelete.RemoveAll(crn => task.CRNsToDelete.Contains(crn));

                                    // Add registered CRNs to list of crnsToDelete. Need to re-register 
                                    // if higher priority found.
                                    var crnsToDelete = new List<CRNToDelete>(task.CRNsToAdd.Count());
                                    foreach (var crnToDelete in task.CRNsToAdd) {
                                        var item = db.CRNsToDelete.Local.SingleOrDefault(crn => crn.Number == crnToDelete.Number);
                                        if (item == null) {
                                            item = new CRNToDelete() { Number = crnToDelete.Number };
                                            db.CRNsToDelete.Add(item);
                                        }
                                        crnsToDelete.Add(item);
                                    }
                                    otherTask.CRNsToDelete.AddRange(crnsToDelete);

                                    db.Entry(otherTask).State = EntityState.Modified;
                                }
                            }

                            // Add to log.
                            log.Info("Task " + task.Id + " with name \"" + task.Group.Name
                                + "\" completed with status " + task.Status + " and with message \"" 
                                + task.StatusDetails + "\"");
                        }
                    } else {
                        db.SaveChanges();
                        return;
                    }
                }
                db.SaveChanges();
            }
            RefreshAllRequest.Set();
            DataContext.UpdateInputFile();
        }

        public static void TerminateAll() {
            TerminateRequest.Reset();
            foreach (var task in MonitoringTasks.Values)
                task.TokenSource.Cancel();
            try {
                Task.WaitAll(MonitoringTasks.Values.Select(t => t.MonitorTask).ToArray());
            }
            catch { }
            while (MonitoringTasks.Values.Any(t => !t.MonitorTask.IsCompleted));
            MonitoringTasks.Clear();

            int unused;
            while (!CRNsToRestart.IsEmpty)
                CRNsToRestart.TryDequeue(out unused);
            while (!TasksToRegister.IsEmpty)
                TasksToRegister.TryDequeue(out unused);
        }

        public static void Init() {
            RestartRequest.Reset();
            RefreshAllRequest.Reset();
            TerminateRequest.Reset();
            RegisterRequest.Reset();
        }

        public static void ManageTasks() {
            var handles = new WaitHandle[] { TerminateRequest, RestartRequest, RegisterRequest, RefreshAllRequest};
            while (true) {
                int type = WaitHandle.WaitAny(handles);

                // From docs: "If more than one object becomes signaled during the call, 
                // the return value is the array index of the signaled object with the 
                // smallest index value of all the signaled objects."
                switch (type) {
                    case 0:
                        TerminateAll();
                        return;
                    case 1:
                        RestartRequestedCRNs();
                        break;
                    case 2:
                        Register();
                        break;
                    case 3:
                        RefreshRunningTasks();
                        break;
                }
            }
        }
    }
}
