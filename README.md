This is a web app for Purdue students to automatically register for classes as soon as they become available. It pings Purdue servers periodically to check availability of courses specified in a config file (TODO: needs to be configurable via the web interface).

Course availabilities are checked asynchronously on a single thread using asynchronous programming. This is to prevent overload on Purdue servers. Once a course is available, the application logs in to myPurdue as the user (encrypted credentials are stored in the config), and registers for the class on behalf of the user. The requests are timed carefully since quick successive requests alarms the myPurdue system.