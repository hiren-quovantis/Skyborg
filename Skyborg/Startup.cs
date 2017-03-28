using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using Hangfire;

[assembly: OwinStartup(typeof(Skyborg.Startup))]

namespace Skyborg
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=316888
            app.UseHangfireDashboard();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions());
        }
    }
}
