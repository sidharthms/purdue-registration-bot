using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Xml;
using System.Xml.Linq;
using MvcApplication45.Models;
using MvcApplication45.Common;
using System.Web.Hosting;
using System.Transactions;

namespace MvcApplication45.DAL {
    public class DataContext : EFDataContext {
        public static void UpdateInputFile() {
            var doc = new XDocument();

            var usersNode = new XElement("users");
            doc.Add(usersNode);
            using (var db = new DataContext()) {
                foreach (var user in db.UserData) {
                    var userNode = new XElement("user",
                        new XElement("name", user.Name),
                        new XElement("email", user.Email),
                        new XElement("username", user.PUusername),
                        new XElement("password", user.PUpassword),
                        new XElement("pin", user.RegPIN),
                        new XElement("groups"));
                    var groupsNode = userNode.Element("groups");
                    usersNode.Add(userNode);
                    foreach (var group in user.TaskGroups) {
                        var groupNode = new XElement("group",
                            new XElement("id", group.Id),
                            new XElement("name", group.Name),
                            new XElement("termid", group.TermId),
                            new XElement("tasks"));
                        var tasksNode = groupNode.Element("tasks");
                        groupsNode.Add(groupNode);
                        foreach (var task in group.RegTasks) {
                            var taskNode = new XElement("task",
                                new XElement("id", task.Id),
                                new XElement("course", task.Course),
                                new XElement("notifyonly", task.NotifyOnly.ToString()),
                                new XElement("priority", task.Priority),
                                new XElement("interval", task.CheckInterval),
                                new XElement("add"),
                                new XElement("delete"));
                            if (task.Status == RegTask.RegStatus.Complete)
                                taskNode.Element("course").AddAfterSelf(
                                    new XElement("note", "This task will no longer be updated"),
                                    new XElement("status", task.Status.ToString()),
                                    new XElement("statusdetails", task.StatusDetails));
                            var addNode = taskNode.Element("add");
                            var deleteNode = taskNode.Element("delete");
                            tasksNode.Add(taskNode);
                            foreach (var crn in task.CRNsToAdd) {
                                var crnNode = new XElement("crn", crn.Number);
                                addNode.Add(crnNode);
                            }
                            foreach (var crn in task.CRNsToDelete) {
                                var crnNode = new XElement("crn", crn.Number);
                                deleteNode.Add(crnNode);
                            }
                        }
                    }
                }
            }
            doc.Save(HostingEnvironment.MapPath("~/App_Data/input.xml"));
        }

        public static void ClearDb() {
            //using (var db = new EFDataContext()) {
            //    db.Database.ExecuteSqlCommand("TRUNCATE TABLE UserData");
            //}
        }

        public static void UpdateDb(bool clearDb = false) {
            XmlDocument doc = new XmlDocument();
            doc.Load(HostingEnvironment.MapPath("~/App_Data/input.xml"));

            ClearDb();
            using (var db = new DataContext()) {
                foreach (XmlNode userNode in doc.SelectSingleNode("users")) {
                    UserInfo user;
                    var username = userNode.SelectSingleNode("username").InnerText;
                    if (!db.UserData.Any(u => u.PUusername == username)) {
                        user = new UserInfo() { PUusername = username };
                        var passwordNode = userNode.SelectSingleNode("password");
                        if (passwordNode == null) {
                            user.PUpassword = "";

                            // Add to input file.
                            var newNode = doc.CreateElement("password");
                            newNode.AppendChild(doc.CreateTextNode(user.PUpassword));
                            userNode.InsertAfter(newNode, userNode.SelectSingleNode("username"));
                        }
                        else {
                            user.PUpassword = passwordNode.InnerText;
                        }
                        db.UserData.Add(user);
                    }
                    else {
                        user = db.UserData.Single(u => u.PUusername == username);
                    }
                    user.Name = userNode.SelectSingleNode("name").InnerText;
                    user.Email = userNode.SelectSingleNode("email").InnerText;
                    user.RegPIN = int.Parse(userNode.SelectSingleNode("pin").InnerText);

                    if (user.TaskGroups == null)
                        user.TaskGroups = new List<RegTaskGroup>();
                    var groupsNow = new List<RegTaskGroup>();
                    foreach (XmlNode groupNode in userNode.SelectSingleNode("groups").ChildNodes) {
                        RegTaskGroup group;
                        var groupId = groupNode.SelectSingleNode("id");
                        if (groupId == null || !user.TaskGroups.Any(g => g.Id == int.Parse(groupId.InnerText))) {
                            group = new RegTaskGroup();
                            user.TaskGroups.Add(group);
                        }
                        else {
                            group = user.TaskGroups.Single(g => g.Id == int.Parse(groupId.InnerText));
                        }
                        groupsNow.Add(group);
                        group.Name = groupNode.SelectSingleNode("name").InnerText;
                        group.TermId = int.Parse(groupNode.SelectSingleNode("termid").InnerText);

                        if (group.RegTasks == null)
                            group.RegTasks = new List<RegTask>();
                        var tasksNow = new List<RegTask>();
                        foreach (XmlNode taskNode in groupNode.SelectSingleNode("tasks")) {
                            RegTask task;
                            var taskId = taskNode.SelectSingleNode("id");
                            if (taskId == null || !group.RegTasks.Any(t => t.Id == int.Parse(taskId.InnerText))) {
                                task = new RegTask();
                                group.RegTasks.Add(task);
                            }
                            else {
                                task = group.RegTasks.Single(t => t.Id == int.Parse(taskId.InnerText));
                            }
                            tasksNow.Add(task);
                            var notifyNode = taskNode.SelectSingleNode("notifyonly");
                            if (notifyNode != null)
                                task.NotifyOnly = bool.Parse(notifyNode.InnerText);
                            else
                                task.NotifyOnly = false;
                            task.Course = taskNode.SelectSingleNode("course").InnerText;
                            task.Priority = int.Parse(taskNode.SelectSingleNode("priority").InnerText);
                            task.CheckInterval = int.Parse(taskNode.SelectSingleNode("interval").InnerText);
                            if (task.Status == RegTask.RegStatus.Complete)
                                continue;
                            var statusNode = taskNode.SelectSingleNode("status");
                            if (statusNode != null) {
                                var status = (RegTask.RegStatus)Enum.Parse(
                                    typeof(RegTask.RegStatus), statusNode.InnerText);
                                if (status != task.Status) {
                                    task.Status = status;
                                    task.LastStatusChangeTime = DateTime.Now;
                                }
                            }
                            else
                                task.Status = RegTask.RegStatus.Incomplete;
                            if (task.LastStatusChangeTime == null)
                                task.LastStatusChangeTime = DateTime.Now;

                            if (task.CRNsToAdd == null)
                                task.CRNsToAdd = new List<CRNToAdd>();
                            var toAddNow = new List<int>(taskNode.SelectSingleNode("add").ChildNodes.Count);
                            foreach (XmlNode crnAddNode in taskNode.SelectSingleNode("add")) {
                                var number = int.Parse(crnAddNode.InnerText);
                                toAddNow.Add(number);
                                var allCrnsToAdd = db.CRNsToAdd.AsEnumerable().Union(db.CRNsToAdd.Local);
                                var crn = allCrnsToAdd.SingleOrDefault(c => c.Number == number);
                                task.CRNsToAdd.Add(crn ?? new CRNToAdd() { Number = number });
                            }
                            // Remove obsolete crns to add..
                            var invalidToAdd = task.CRNsToAdd.Where(c => !toAddNow.Contains(c.Number));
                            task.CRNsToAdd.RemoveAll(c => invalidToAdd.Contains(c));

                            if (task.CRNsToDelete == null)
                                task.CRNsToDelete = new List<CRNToDelete>();
                            var toDeleteNow = new List<int>();
                            if (taskNode.SelectSingleNode("delete") != null) {
                                toDeleteNow.Capacity = taskNode.SelectSingleNode("delete").ChildNodes.Count;
                                foreach (XmlNode crnDeleteNode in taskNode.SelectSingleNode("delete")) {
                                    var number = int.Parse(crnDeleteNode.InnerText);
                                    toDeleteNow.Add(number);
                                    var allCrnsToDelete = db.CRNsToDelete.AsEnumerable().Union(db.CRNsToDelete.Local);
                                    var crn = allCrnsToDelete.SingleOrDefault(c => c.Number == number);
                                    task.CRNsToDelete.Add(crn ?? new CRNToDelete() { Number = number });
                                }
                            }
                            // Remove obsolete crns to delete.
                            var invalidToDelete = task.CRNsToDelete.Where(c => !toDeleteNow.Contains(c.Number));
                            task.CRNsToDelete.RemoveAll(c => invalidToDelete.Contains(c));
                        }
                        // Remove obsolete tasks.
                        var invalidTasks = group.RegTasks.Where(t => !tasksNow.Contains(t));
                        foreach (var invalidTask in invalidTasks)
                            if (invalidTask.Status == RegTask.RegStatus.Incomplete) {
                                invalidTask.Status = RegTask.RegStatus.Cancelled;
                                invalidTask.LastStatusChangeTime = DateTime.Now;
                            }
                    }
                    // Remove obsolete groups.
                    var invalidGroups = user.TaskGroups.Where(g => !groupsNow.Contains(g));
                    foreach (var invalidGroup in invalidGroups)
                        foreach (var invalidTask in invalidGroup.RegTasks)
                            if (invalidTask.Status == RegTask.RegStatus.Incomplete) {
                                invalidTask.Status = RegTask.RegStatus.Cancelled;
                                invalidTask.LastStatusChangeTime = DateTime.Now;
                            }

                }

                db.SaveChanges();
            }
            UpdateInputFile();
        }

        public static void Setup() {
            Database.SetInitializer(new DropCreateDatabaseAlways<DataContext>());
            using (var db = new DataContext()) {
                db.Database.Initialize(false);
            }
            DataContext.UpdateDb();
        }
    }

    public class InMemoryDbSet<T> : IDbSet<T> where T : class {

        readonly HashSet<T> _set;
        readonly IQueryable<T> _queryableSet;


        public InMemoryDbSet() : this(Enumerable.Empty<T>()) { }

        public InMemoryDbSet(IEnumerable<T> entities) {
            _set = new HashSet<T>();

            foreach (var entity in entities) {
                _set.Add(entity);
            }

            _queryableSet = _set.AsQueryable();
        }

        public T Add(T entity) {
            _set.Add(entity);
            return entity;

        }

        public T Attach(T entity) {
            _set.Add(entity);
            return entity;
        }

        public TDerivedEntity Create<TDerivedEntity>() where TDerivedEntity : class, T {
            throw new NotImplementedException();
        }

        public T Create() {
            throw new NotImplementedException();
        }

        public T Find(params object[] keyValues) {
            throw new NotImplementedException();
        }

        public System.Collections.ObjectModel.ObservableCollection<T> Local {
            get { return new System.Collections.ObjectModel.ObservableCollection<T>(_set); }
        }

        public T Remove(T entity) {
            _set.Remove(entity);
            return entity;
        }

        public IEnumerator<T> GetEnumerator() {
            return _set.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public Type ElementType {
            get { return _queryableSet.ElementType; }
        }

        public System.Linq.Expressions.Expression Expression {
            get { return _queryableSet.Expression; }
        }

        public IQueryProvider Provider {
            get { return _queryableSet.Provider; }
        }
    }

    public class LocalDataContext : IDisposable {
        static InMemoryDbSet<UserInfo> SavedUserData = new InMemoryDbSet<UserInfo>();
        static InMemoryDbSet<RegTaskGroup> SavedRegTaskGroups = new InMemoryDbSet<RegTaskGroup>();
        static InMemoryDbSet<RegTask> SavedRegTasks = new InMemoryDbSet<RegTask>();
        static InMemoryDbSet<CRNToAdd> SavedCRNsToAdd = new InMemoryDbSet<CRNToAdd>();
        static InMemoryDbSet<CRNToDelete> SavedCRNsToDelete = new InMemoryDbSet<CRNToDelete>();

        public InMemoryDbSet<UserInfo> UserData { get; set; }
        public InMemoryDbSet<RegTaskGroup> RegTaskGroups { get; set; }
        public InMemoryDbSet<RegTask> RegTasks { get; set; }
        public InMemoryDbSet<CRNToAdd> CRNsToAdd { get; set; }
        public InMemoryDbSet<CRNToDelete> CRNsToDelete { get; set; }

        DbEntityEntry dummyEntry { get; set; }

        public LocalDataContext() {
            UserData = SavedUserData;
            RegTaskGroups = SavedRegTaskGroups;
            RegTasks = SavedRegTasks;
            CRNsToAdd = SavedCRNsToAdd;
            CRNsToDelete = SavedCRNsToDelete;
        }
        public DbEntityEntry Entry(object obj) {
            return dummyEntry;
        }
        public void Dispose() {
        }
        public void SaveChanges() { 

}

        //public LocalDataContext Include<T, TProperty>(
        //    Expression<Func<T, TProperty>> path
        //) where T : class {
        //    return this;
        //}
    }

    public class EFDataContext : DbContext {
        public DbSet<UserInfo> UserData { get; set; }
        public DbSet<RegTaskGroup> RegTaskGroups { get; set; }
        public DbSet<RegTask> RegTasks { get; set; }
        public DbSet<CRNToAdd> CRNsToAdd { get; set; }
        public DbSet<CRNToDelete> CRNsToDelete { get; set; }

        public EFDataContext() : base("DAL.DataContext") {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder) {
            //configure model with fluent API
            modelBuilder.Entity<RegTask>().HasMany(t => t.CRNsToAdd).WithMany(c => c.RegTasks);
            modelBuilder.Entity<RegTask>().HasMany(t => t.CRNsToDelete).WithMany();
        }
    }

    public class CreateDatabaseTables<T> : IDatabaseInitializer<T> where T : DbContext {
        public void InitializeDatabase(T context) {
            bool dbExists;
            using (new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress)) {
                dbExists = context.Database.Exists();
            }
            if (dbExists) {
                // create all tables
                var dbCreationScript = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)context).ObjectContext.CreateDatabaseScript();
                context.Database.ExecuteSqlCommand(dbCreationScript);

                context.SaveChanges();
            }
            else {
                throw new ApplicationException("No database instance");
            }
        }
    }
}
