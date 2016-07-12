using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace MvcApplication45.Models {
    public class UserInfo {
        /// <summary>
        /// Career account alias
        /// </summary>
        [Key]
        public string PUusername { get; set; }

        /// <summary>
        /// Encrypted Career account password
        /// </summary>
        public string PUpassword { get; set; }

        /// <summary>
        /// Preferred email
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Name of user
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Registration PIN
        /// </summary>
        public int RegPIN { get; set; }

        /// <summary>
        /// Tasks associated with this user.
        /// </summary>
        public virtual List<RegTaskGroup> TaskGroups { get; set; }
    }
}
