using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Threading;
using System.Threading.Tasks;
using MvcApplication45.Common;
using MvcApplication45.DAL;
using log4net;
using System.Configuration;

namespace MvcApplication45 {
    public class Program {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static int FailedCount { get; set; }

        public static readonly int CheckRetryInterval = 10000;
        public static readonly int CheckRetryCount = 100;
        public static readonly int RegRetryInterval = 10000;
        public static readonly int RegRetryCount = 100;
        public static readonly int PriorityScaler = 30000;

        public static readonly ManualResetEvent AllDone = new ManualResetEvent(false);

        public static void TerminateAll() {
            if (!QueueUtility.TerminateRequest.WaitOne(0) && QueueUtility.QueueingThread != null) {
                QueueUtility.TerminateRequest.Set();
                QueueUtility.QueueingThread.Join();
                QueueUtility.QueueingThread = null;
                DataContext.UpdateInputFile();
            }
        }

        private static List<Timer> pingTimer = new List<Timer>();

        public static string[] urls;
        public static void PingSite(Object link) {
            var result = ScrapeUtility.FetchPageByGet((string)link);
            log.Info("Sent request to " + link + " ; response " + result.Substring(0, 20));
        }

        public static void GenerateTraffic() {
            urls = ConfigurationManager.AppSettings["urls"].Split(';');
            var gap = 4.9;
            foreach (var url in urls) {
                pingTimer.Add(new Timer(PingSite, url, TimeSpan.Zero, TimeSpan.FromMinutes(gap)));
                gap += 0.8;
            }
        }

        public static void Start() {
            if (QueueUtility.QueueingThread != null)
                return;

            System.Net.ServicePointManager.ServerCertificateValidationCallback
                = ((sender, cert, chain, errors) => true);
            System.Net.ServicePointManager.Expect100Continue = false;
            log4net.Config.XmlConfigurator.Configure();

            log.Info("App Starting");

            DataContext.Setup();
            GenerateTraffic();

            var dataValid = RegistrationUtility.VerifyData().Result;
            if (!dataValid)
                throw new InvalidDataException("Invalid Data");

            // Send test email.
            if (!EmailUtility.SendStartEmail()) {
                throw new Exception("Invalid Data");
            }

            FailedCount = 0;

            QueueUtility.Init();
            QueueUtility.QueueingThread = new Thread(QueueUtility.ManageTasks);
            QueueUtility.QueueingThread.Start();
            QueueUtility.RefreshAllRequest.Set();
        }
    }
}
