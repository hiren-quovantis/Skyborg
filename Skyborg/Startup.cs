using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using Hangfire;
using Skyborg.Dialogs;
using Hangfire.Common;

[assembly: OwinStartup(typeof(Skyborg.Startup))]

namespace Skyborg
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseHangfireDashboard("/hangfire", new DashboardOptions());

            var manager = new RecurringJobManager();
            manager.AddOrUpdate("calendarSchedule", Job.FromExpression(() => new CalendarDialog().PushDailySchedule()), "*/2 * * * *");

            app.UseHangfireServer();
            
        }
    }
}
