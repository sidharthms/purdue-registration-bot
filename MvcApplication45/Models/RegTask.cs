using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcApplication45.Models {
    public class RegTask {
        public enum RegStatus {
            Incomplete,
            Complete,
            AlternateComplete,
            Cancelled,
            FailedWithRestoreSuccess,
            FailedWithRestoreFail,
            Failed,
            InvalidData
        }

        public int Id { get; set; }

        /// <summary>
        /// Used for change tracking.
        /// </summary>
        public string StringId { get; set; }

        /// <summary>
        /// Identifying name for task.
        /// </summary>
        public string Course { get; set; }

        /// <summary>
        /// Don't register only send email notification.
        /// </summary>
        public bool NotifyOnly { get; set; }

        /// <summary>
        /// The priority of this task. When multiple tasks are are set to 
        /// monitor a common CRN tasks with a lower value are registered 
        /// first.
        /// </summary>
        public int Priority { get; set; }

        public int CheckInterval { get; set; }

        /// <summary>
        /// Indicates whether the task has been completed successfully.
        /// </summary>
        public RegStatus Status { get; set; }

        public string StatusDetails { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime? LastStatusChangeTime { get; set; }

        /// <summary>
        /// Group that the task belongs to.
        /// </summary>
        public virtual RegTaskGroup Group { get; set; }

        /// <summary>
        /// CRNs to drop when CRNs to add become available. In case of failure 
        /// to add the application will attempt to add these back to schedule.
        /// </summary>
        public virtual List<CRNToDelete> CRNsToDelete { get; set; }

        /// <summary>
        /// When all of these CRNs become available the CRNsToDelete will be 
        /// dropped and the application will attempt to add these.
        /// </summary>
        public virtual List<CRNToAdd> CRNsToAdd { get; set; }
    }
}
