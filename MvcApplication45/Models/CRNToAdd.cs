using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcApplication45.Models {
    public class CRNToAdd : CRN {
        /// <summary>
        /// Class Registration Number.
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Number { get; set; }

        /// <summary>
        /// Tasks monitoring this CRN.
        /// </summary>
        public List<RegTask> RegTasks { get; set; }

        /// <summary>
        /// Space available.
        /// </summary>
        [NotMapped]
        public int SpaceAvailable { get; set; }
    }
}
