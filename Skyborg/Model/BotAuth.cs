using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Skyborg.Model
{
    public class BotAuth
    {
        public string UserId { get; set; }
        public string BotId { get; set; }
        public string ConversationId { get; set; }
        public string ServiceUrl { get; set; }
        public string ChannelId { get; set; }
        public string ActivityId { get; set; }

    }
}