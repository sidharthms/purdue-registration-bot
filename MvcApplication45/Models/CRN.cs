using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcApplication45.Models {
    public interface CRN {
        /// <summary>
        /// Class Registration Number.
        /// </summary>
        int Number { get; set; }
    }
}
