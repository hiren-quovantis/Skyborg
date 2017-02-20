using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Skyborg.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Skyborg.Adapters
{
    [Serializable]
    public class CalendarAdapter
    {
        [NonSerialized]
        CalendarService service;
        public CalendarAdapter(UserCredential credential)
        {
            service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Skyborg"
            });
        }

        public Events GetEventsByDateRange(DateTime startdate, DateTime enddate)
        {
            string response = string.Empty;

            EventsResource.ListRequest request = service.Events.List("primary");
            request.TimeMin = DateTime.Now;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.MaxResults = 10;
            request.TimeMax = enddate;
            request.TimeMin = startdate;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            return request.Execute();
        }

        public Event CreateEvent(CalendarModel calendarDetail)
        {
            Event calendarEvent = new Event();

            calendarEvent.Summary = calendarDetail.Summary;
            calendarEvent.Location = calendarDetail.Location;

            calendarEvent.Start = new EventDateTime();
            calendarEvent.Start.DateTime = calendarDetail.StartDate.Add(calendarDetail.StartTime);

            calendarEvent.End = new EventDateTime();
            calendarEvent.End.DateTime = calendarEvent.Start.DateTime.Value.AddHours(1);
            
            calendarEvent.Reminders = new Event.RemindersData();
            calendarEvent.Reminders.UseDefault = true;

            if (calendarDetail.Attendees != null && calendarDetail.Attendees.Count > 0)
            {
                calendarEvent.Attendees = new List<EventAttendee>();
                foreach (string attendee in calendarDetail.Attendees)
                {
                    EventAttendee eventAttendee = new EventAttendee();
                    eventAttendee.Email = attendee;
                    calendarEvent.Attendees.Add(eventAttendee);
                }
            }

            try
            {
                EventsResource.InsertRequest request = service.Events.Insert(calendarEvent, "primary");
                return request.Execute();
            }
            catch(Exception e)
            {
                throw e;
            }
            

        }
    }
}