using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace Skyborg.Common
{
    public static class BotConstants
    {
        public static string LUISSubscriptionKey
        {
            get
            {
                return Convert.ToString(ConfigurationManager.AppSettings["LUISSubscriptionKey"]);
            }
        }

        public static string LUISModelId
        {
            get
            {
                return Convert.ToString(ConfigurationManager.AppSettings["LUISModelId"]);
            }
        }

        public static string GoogleClientId
        {
            get
            {
                return Convert.ToString(ConfigurationManager.AppSettings["GoogleClientId"]);
            }
        }

        public static string GoogleClientSecret
        {
            get
            {
                return Convert.ToString(ConfigurationManager.AppSettings["GoogleClientSecret"]);
            }
        }
    }
}