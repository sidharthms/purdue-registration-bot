using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcApplication45.Models {
    public class RegTaskGroup {
        public int Id { get; set; }

        /// <summary>
        /// Identifying name for task group.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Banner Id for the term to register for.
        /// </summary>
        public int TermId { get; set; }

        /// <summary>
        /// User associated with task.
        /// </summary>
        public virtual UserInfo User { get; set; }

        public virtual List<RegTask> RegTasks { get; set; }
    }
}
