using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Web;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MvcApplication45.Models;
using MvcApplication45.DAL;

namespace MvcApplication45.Common {
    public class RegistrationUtility {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string pinSubmitUrl = "https://selfservice.mypurdue.purdue.edu/prod/bwskfreg.P_CheckAltPin";
        private static readonly string addDropUrl = "https://selfservice.mypurdue.purdue.edu/prod/bwckcoms.P_Regs";

        public static int? CheckCourseAvailable(int CRN, int termCode) {
            log.Info("Checking availability of " + CRN);
            string URL = "https://selfservice.mypurdue.purdue.edu/prod/bwckschd.p_disp_detail_sched?term_in="
                + termCode + "&crn_in=" + CRN;

            int remaining;
            // Maybe CRN and/or term might be incorrect
            try {
                string html = ScrapeUtility.FetchPageByGet(URL);
                remaining = int.Parse(Regex.Match(html, @"(\d*)</TD>.*?Waitlist Seats",
                    RegexOptions.Singleline | RegexOptions.RightToLeft).Groups[1].Value);
            }
            catch {
                return null;
            }
            return remaining;
        }

        public static async Task<Tuple<CookieContainer, string>> LoginToAddDropPage(
                string username, string password, int term, int regPIN) {

            var cookieJar = new CookieContainer();
            var loginHTML = ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/cp/home/displaylogin", cookieJar);
            var uuid = long.Parse(Regex.Match(loginHTML,
                @"var clientServerDelta = \(new Date\(\)\).getTime\(\) \- (\d*);").Groups[1].Value);
            await Task.Delay(2000);

            var page1HTML = ScrapeUtility.FetchPageByPost("https://wl.mypurdue.purdue.edu/cp/home/login",
                "pass=" + HttpUtility.UrlEncode(CipherUtility.StandardDecrypt(password)) 
                    + "&user=" + username + "&uuid=" + (uuid + 2150),
                "https://wl.mypurdue.purdue.edu/cp/home/displaylogin",
                cookieJar);
            if (page1HTML.Contains("username/password pair not found"))
                throw new InvalidDataException("Invalid username/password");
            var page2HTML = ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/cps/welcome/loginok.html",
                cookieJar,
                "https://wl.mypurdue.purdue.edu/cp/home/displaylogin");
            var page3HTML = ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/cp/home/next",
                cookieJar,
                "https://wl.mypurdue.purdue.edu/cps/welcome/loginok.html",
                ScrapeUtility.RedirectType.CustomRedirect);
            //var page4HTML = ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/render.userLayoutRootNode.uP?uP_root=root",
            //    cookieJar,
            //    "https://wl.mypurdue.purdue.edu/cps/welcome/loginok.html");
            if (page3HTML.Contains("https://wl.mypurdue.purdue.edu/jsp/misc/timedout.jsp"))
                throw new WebException("Timed Out");
            await Task.Delay(1000);

            var addDrop1 = ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/render.UserLayoutRootNode.uP?uP_tparam=utf&utf=%2fcp%2fip%2flogin%3fsys%3dsctssb%26url%3dhttps://selfservice.mypurdue.purdue.edu/prod/tzwkwbis.P_CheckAgreeAndRedir?ret_code=STU_ADDDROP",
                cookieJar,
                "https://wl.mypurdue.purdue.edu/tag.162379192633c61c.render.userLayoutRootNode.uP?uP_root=root&uP_sparam=activeTab&activeTab=u12l1s2&uP_tparam=frm&frm=");
            var addDrop2 = ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/cp/ip/login?sys=sctssb&url=https://selfservice.mypurdue.purdue.edu/prod/tzwkwbis.P_CheckAgreeAndRedir?ret_code=STU_ADDDROP",
                cookieJar,
                "https://wl.mypurdue.purdue.edu/render.UserLayoutRootNode.uP?uP_tparam=utf&utf=%2fcp%2fip%2flogin%3fsys%3dsctssb%26url%3dhttps://selfservice.mypurdue.purdue.edu/prod/tzwkwbis.P_CheckAgreeAndRedir?ret_code=STU_ADDDROP",
                ScrapeUtility.RedirectType.CustomRedirect); // Custom Redirection.
            var addDrop3 = ScrapeUtility.FetchPageByGet("https://selfservice.mypurdue.purdue.edu/prod/tzwkwbis.P_CheckAgreeAndRedir?ret_code=STU_ADDDROP",
                cookieJar,
                "https://wl.mypurdue.purdue.edu/render.UserLayoutRootNode.uP?uP_tparam=utf&utf=%2fcp%2fip%2flogin%3fsys%3dsctssb%26url%3dhttps://selfservice.mypurdue.purdue.edu/prod/tzwkwbis.P_CheckAgreeAndRedir?ret_code=STU_ADDDROP");
            var addDrop4 = ScrapeUtility.FetchPageByGet("https://selfservice.mypurdue.purdue.edu/prod/bwskfreg.P_AltPin",
                cookieJar,
                "https://selfservice.mypurdue.purdue.edu/prod/tzwkwbis.P_CheckAgreeAndRedir?ret_code=STU_ADDDROP");
            await Task.Delay(1000);

            var pinEnterPage = ScrapeUtility.FetchPageByPost("https://selfservice.mypurdue.purdue.edu/prod/bwskfreg.P_AltPin",
                "term_in=" + term,
                "https://selfservice.mypurdue.purdue.edu/prod/bwskfreg.P_AltPin", cookieJar);
            await Task.Delay(1000);

            var addDropPage = ScrapeUtility.FetchPageByPost(pinSubmitUrl,
                "pin=" + regPIN,
                "https://selfservice.mypurdue.purdue.edu/prod/bwskfreg.P_AltPin", 
                cookieJar);
            if (addDropPage.Contains("Invalid Registration PIN"))
                throw new InvalidDataException("Invalid Registration PIN");
            if (!addDropPage.Contains("Add or Drop Classes"))
                throw new WebException("Did not land on add drop page :(");

            return new Tuple<CookieContainer,string>(cookieJar, addDropPage);
        }

        private static bool VerifyAddDropPage(CookieContainer cookieJar) {
            return true;
        }

        private static void DropCRNs(List<CRN> crns, CookieContainer cookieJar, ref string addDropHtml, ref string referer) {
            if (crns.Count == 0)
                return;

            var dataToEnter = new Dictionary<string, string>();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(addDropHtml);

            for (int i = 1; i <= crns.Count; ++i) {
                var index = doc.DocumentNode.SelectNodes("//table[@class='datadisplaytable']//input[@name='CRN_IN']")
                    .ToList().FindIndex(n => n.Attributes["value"].Value == crns[i - 1].Number.ToString());
                if (index >= 0)
                    dataToEnter.Add("action_id" + (index + 1), "DW");
            }
            if (dataToEnter.Keys.Count == 0)
                return;

            string postBody;
            try {
                postBody = ScrapeUtility.PostDataFromForm(addDropHtml, dataToEnter);
            }
            catch {
                throw new InvalidDataException("Invalid drop CRN(s)");
            }
            var correctedPostBody = postBody.Substring(postBody.IndexOf("&") + 1,
                postBody.LastIndexOf("&") - postBody.IndexOf("&") - 1);

            var deleteResult = ScrapeUtility.FetchPageByPost(
                addDropUrl, correctedPostBody, referer, cookieJar);
            addDropHtml = deleteResult;
            referer = addDropUrl;
        }

        private static string AddCRNs(List<CRN> crns, CookieContainer cookieJar, ref string addDropHtml, ref string referer) {
            string details;
            var dataToEnter = new Dictionary<string, string>();
            if (crns.Count > 10)
                throw new InvalidDataException("Cannot register more than 10 classes at once!");

            for (int i = 1; i <= crns.Count; ++i)
                dataToEnter.Add("crn_id" + i, crns[i - 1].Number.ToString());

            var postBody = ScrapeUtility.PostDataFromForm(addDropHtml, dataToEnter);
            var correctedPostBody = postBody.Substring(postBody.IndexOf("&") + 1, 
                postBody.LastIndexOf("&") - postBody.IndexOf("&") - 1);

            var addResult = ScrapeUtility.FetchPageByPost(addDropUrl,
                correctedPostBody,
                referer,
                cookieJar);

            addDropHtml = addResult;
            referer = addDropUrl;
            if (addResult.Contains("Registration Add Errors")) {
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(addDropHtml);

                details = "Registration Add Errors";
                foreach (var row in doc.DocumentNode.SelectNodes("(//table[@class='datadisplaytable'])[2]/tr").Skip(1)) {
                    details += "\n" + row.ChildNodes[3].InnerText + " : " + row.ChildNodes[1].InnerText;
                }
            }
            else
                details = "Success";
            return details;
        }

        public static async Task<Tuple<RegTask.RegStatus, string>> TryRegisterCourse(RegTask task) {
            log.Info("Attempting to register for task " + task.Id + " in group " + task.Group.Name);
            var sent = EmailUtility.SendSpaceNotification(task);
            if (task.NotifyOnly) {
                return new Tuple<RegTask.RegStatus,string>(
                    sent ? RegTask.RegStatus.Complete : RegTask.RegStatus.Failed, 
                    sent ? "Email sent successfully" : "Email send failed");
            }

            CookieContainer cookieJar = null;
            RegTask.RegStatus status = RegTask.RegStatus.Incomplete;
            string addDropHtml;
            string referer = pinSubmitUrl;
            string addDetails = "";

            var crnsToDelete = new List<CRN>(task.CRNsToDelete);
            var crnsToAdd = new List<CRN>(task.CRNsToAdd);

            for (int count = 0; count < Program.RegRetryCount; ++count) {
                if (count > 0)
                    await Task.Delay(Program.RegRetryInterval);
                try {
                    cookieJar = null;
                    var loginResults = await LoginToAddDropPage(
                        task.Group.User.PUusername,
                        task.Group.User.PUpassword,
                        task.Group.TermId,
                        task.Group.User.RegPIN);
                    cookieJar = loginResults.Item1;
                    addDropHtml = loginResults.Item2;

                    if (VerifyAddDropPage(cookieJar)) {
                        // Drop requested crns.
                        DropCRNs(crnsToDelete, cookieJar, ref addDropHtml, ref referer);

                        // Add requested crns.
                        addDetails = AddCRNs(crnsToAdd, cookieJar, ref addDropHtml, ref referer);
                        if (addDetails != "Success") {
                            if (crnsToDelete.Any()) {
                                var reAddStatus = AddCRNs(
                                    crnsToDelete, cookieJar, ref addDropHtml, ref referer);
                                status = reAddStatus == "Success" ? 
                                    RegTask.RegStatus.FailedWithRestoreSuccess :
                                    RegTask.RegStatus.FailedWithRestoreFail;
                                if (reAddStatus != "Success")
                                    addDetails += "\n\nRe-Add Dropped classes failed:\n";
                            }
                            else {
                                status = RegTask.RegStatus.Failed;
                            }
                        }
                        else {
                            status = RegTask.RegStatus.Complete;
                        }
                        break;
                    }
                }
                catch (InvalidDataException e) {
                    status = RegTask.RegStatus.Incomplete;
                    addDetails += e.Message;
                    break;
                }
                catch { 
                    log.Error("Unknown Exception during REGISTRATION");
                } // Ignore other errors errors in logout.
                finally {
                    var logoutHTML = ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/cp/home/logout?uP_tparam=frm&frm=", cookieJar);
                }
            }
            return new Tuple<RegTask.RegStatus, string>(status, addDetails);
        }

        public async static Task<bool> MonitorCRN(int crn, int termId, int interval, CancellationToken ct) {
            log.Info("Task to monitor " + crn + " started");

            int failedTrys = 0;
            while (true) {
                if (failedTrys == Program.CheckRetryCount)
                    return false;
                var result = CheckCourseAvailable(crn, termId);

                /// CRN is invalid or some network error has occured.
                if (result == null) {
                    ++failedTrys;
                    var retryDelayTask = Task.Delay(Program.CheckRetryInterval, ct);
                    await retryDelayTask;
                    if (retryDelayTask.IsCanceled)
                        return false;
                    continue;
                }

                // Class is available.
                if (result.Value >= 1)
                    return true;

                failedTrys = 0;

                var delayTask = Task.Delay(interval, ct);
                await delayTask;
                if (delayTask.IsCanceled)
                    return false;
            }
        }

        public async static Task<bool> VerifyData() {
            return true;
            log.Info("Verifying Data");
            var quit = false;
            using (var db = new DataContext()) {
                foreach (var user in db.UserData) {
                    if (!user.TaskGroups.Any())
                        continue;

                    for (int count = 0; count < Program.RegRetryCount; ++count) {
                        if (count > 0)
                            await Task.Delay(Program.RegRetryInterval);
                        CookieContainer cookieJar = null;

                        try {
                            var loginResults = await LoginToAddDropPage(user.PUusername, user.PUpassword, user.TaskGroups[0].TermId, user.RegPIN);
                            cookieJar = loginResults.Item1;
                            break;
                        }
                        catch (InvalidDataException e) {
                            foreach (var task in db.RegTasks.Where(t => t.Group.User == user)) {
                                task.Status = RegTask.RegStatus.InvalidData;
                                task.StatusDetails = e.Message;
                                task.LastStatusChangeTime = DateTime.Now;
                                db.Entry(task).State = EntityState.Modified;
                            }
                            break;
                        }
                        catch {
                            log.Warn("Unknown Exception during data verification");
                        } // Ignore errors in logout.
                        finally {
                            ScrapeUtility.FetchPageByGet("https://wl.mypurdue.purdue.edu/cp/home/logout?uP_tparam=frm&frm=", cookieJar);
                        }
                    }
                    if (user.TaskGroups[0].RegTasks[0].Status == RegTask.RegStatus.InvalidData) {
                        quit = true;
                        break;
                    }
                }
                db.SaveChanges();
            }
            if (quit)
                log.Error("Data verification failed");
            else
                log.Info("Data verification complete");
            return !quit;
        }
    }
}
