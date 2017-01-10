using Microsoft.Bot.Builder.FormFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Skyborg.Model
{
    [Serializable]
    public class CalendarModel
    {
        [Prompt("Please enter event {&}")]
        public string Summary { get; set; }

        [Prompt("Please enter event {&}")]
        public string Location { get; set; }

        [Prompt("Please enter valid {&}")]
        public DateTime StartDate { get; set; }

        [Prompt("Please enter valid {&}")]
        public TimeSpan StartTime { get; set; }

        //[Prompt("Please enter valid {&}")]
        //public DateTime EndTime { get; set;}

        [Prompt("Please enter valid Email for {&}")]
        public List<string> Attendees { get; set; }
    }
}