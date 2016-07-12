using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MvcApplication45.Common;

namespace MvcApplication45.Controllers
{
    public class RemoteControlController : Controller
    {
        // GET: /RemoteControl/Start
        public ActionResult Start() {
            Program.Start();
            return View("../Home/Index");
        }

        // GET: /RemoteControl/Quit
        public ActionResult Quit() 
        {
            Program.TerminateAll();
            return View();
        }
        
        // GET: /RemoteControl/Reload
        public ActionResult Reload()
        {
            QueueUtility.RefreshAllRequest.Set();
            return View();
        }
	}
}