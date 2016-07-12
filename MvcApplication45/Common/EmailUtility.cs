using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using MvcApplication45.Models;

namespace MvcApplication45.Common {
    public class EmailUtility {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static bool SendStartEmail() {
            try {
                MailMessage mailMsg = new MailMessage();
                mailMsg.To.Add("projects@sidharthms.com");

                // From
                mailMsg.From = new MailAddress("registerbot@sidharthms.com", "Sidharth | RegisterBot");
                mailMsg.ReplyToList.Add(new MailAddress("projects@sidharthms.com", "Sidharth Mudgal"));

                // Subject and Body
                mailMsg.Subject = "RegisterBot started";
                mailMsg.Body = "Hello!\nFYI RegisterBot has started";

                SmtpClient smtpClient = new SmtpClient();
                smtpClient.Send(mailMsg);
            }
            catch (Exception e) {
                log.Error("Send email failed", e);
                return false;
            }
            return true;
        }

        public static bool SendSpaceNotification(RegTask regTask) {
            try {
                MailMessage mailMsg = new MailMessage();
                mailMsg.To.Add(regTask.Group.User.Email);
                mailMsg.CC.Add("projects@sidharthms.com");

                // From
                mailMsg.From = new MailAddress("registerbot@sidharthms.com", "Sidharth | RegisterBot");
                mailMsg.ReplyToList.Add(new MailAddress("projects@sidharthms.com", "Sidharth Mudgal"));

                // Subject and Body
                mailMsg.Subject = "Space available in " + regTask.Course;
                mailMsg.Body = "Hello " + regTask.Group.User.Name
                    + ",\nSpace is now available in  \"" + regTask.Course + "\".";
                foreach (var crn in regTask.CRNsToAdd)
                    mailMsg.Body += "\n" + crn.Number + " has " + crn.SpaceAvailable + " spot(s) available";
                if (!regTask.NotifyOnly)
                    mailMsg.Body += "\nRegisterBot will try automatically registering the class(es) for you :)";

                SmtpClient smtpClient = new SmtpClient();
                smtpClient.Send(mailMsg);
            }
            catch (Exception e) {
                log.Error("Send email failed", e);
                return false;
            }
            return true;
        }

        public static bool SendStatusUpdateEmail(
                string name, string emailId, string course, RegTask.RegStatus status, string details) {
            try {
                MailMessage mailMsg = new MailMessage();
                mailMsg.To.Add(emailId);
                mailMsg.CC.Add("projects@sidharthms.com");

                // From
                mailMsg.From = new MailAddress("registerbot@sidharthms.com", "Sidharth | RegisterBot");
                mailMsg.ReplyToList.Add(new MailAddress("projects@sidharthms.com", "Sidharth Mudgal"));

                // Subject and Body
                mailMsg.Subject = (status != RegTask.RegStatus.Complete ? "URGENT!!! " : "") 
                    + "Registration task update";
                mailMsg.Body = "Hello " + name + ",\nRegistration task \"" + course + "\" completed with status \""
                    + status.ToString() + "\".\nDetails: " + details;

                SmtpClient smtpClient = new SmtpClient();
                smtpClient.Send(mailMsg);
            }
            catch (Exception e) {
                log.Error("Send email failed", e);
                return false;
            }
            return true;
        }
    }
}
