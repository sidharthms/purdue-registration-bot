using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(MvcApplication45.Startup))]
namespace MvcApplication45
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
