using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Hangfire;

namespace Skyborg
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            System.Web.Http.GlobalConfiguration.Configure(WebApiConfig.Register);

            Hangfire.GlobalConfiguration.Configuration.UseSqlServerStorage("SkyborgDataModel");
        }
    }
}
