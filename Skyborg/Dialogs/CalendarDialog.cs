using Microsoft.Bot.Builder.Dialogs;
using System;
using Chronic;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using Skyborg.Model;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Builder.Luis;
using System.Globalization;
using Skyborg.Adapters;
using Microsoft.Bot.Builder.FormFlow;

namespace Skyborg.Dialogs
{
    [LuisModel("e41c3be9-a10d-466c-8dbc-54b64a34b062", "23cc8b8e5d34496aa7e665e8d1bfb7a8")]
    [Serializable]
    public class CalendarDialog : LuisDialog<object>
    {

        private CalendarAdapter adapter;

        public CalendarDialog(UserCredential credential)
        {
            this.adapter = new CalendarAdapter(credential);

        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Sorry! I don't understand what you want");
            context.Wait(MessageReceived);
        }

        [LuisIntent("Get Event List")]
        public async Task GetEventList(IDialogContext context, LuisResult result)
        {
            var dateResult = FetchDate(result);

            if (dateResult != null)
            {
                await context.PostAsync("Please wait, while I retrieve your schedule");

                var reply = context.MakeMessage();

                reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                reply.Attachments = this.GetEventsByDateRange(dateResult.Start.Value, dateResult.End.Value);

                await context.PostAsync(reply);
            }
            else
            {
                await context.PostAsync("Sorry! I don't understand what you want");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("Create Event")]
        public async Task CreateEvent(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Allow me to create an event for you (y)");
            
            CalendarModel eventDetail = new CalendarModel();

            var startDate = FetchDate(result);

            if(startDate != null && startDate.Start.HasValue)
            {
                eventDetail.StartDate = startDate.Start.Value;
            }

            var summary = FetchString(result, "EventName");
            if (!string.IsNullOrEmpty(summary))
            {
                eventDetail.Summary = summary;
            }

            var startTime = FetchTime(result);
            if(startTime != null && startTime.Start.HasValue)
            {
                eventDetail.StartTime = startTime.Start.Value.TimeOfDay;
            }

            //FormDialog<CalendarModel> dialog = new FormDialog<CalendarModel>(eventDetail, this.BuildCreateEventForm, FormOptions.PromptInStart);

            //var dialog = FormDialog.FromForm(this.BuildCreateEventForm, FormOptions.PromptInStart);

            //context.Call<CalendarModel>(dialog, ResumeAfterHotelsFormDialog);

            //context.Call(dialog, this.ResumeAfterHotelsFormDialog);

//            context.Wait(this.MessageReceived);
        }

        public IForm<CalendarModel> BuildCreateEventForm()
        {
            OnCompletionAsyncDelegate<CalendarModel> createEvent = async (context, state) =>
            {
                await context.PostAsync("Creating events ...");
            };

            return new FormBuilder<CalendarModel>()
                        .AddRemainingFields()
                        .OnCompletion(createEvent)
                        .Build(); 
        }

        private async Task ResumeAfterHotelsFormDialog(IDialogContext context, IAwaitable<CalendarModel> result)
        {
            await context.PostAsync("Evenmt Created");
        }

        private IList<Attachment> GetEventsByDateRange(DateTime startdate, DateTime enddate)
        {
            string response = string.Empty;

            Events events = adapter.GetEventsByDateRange(startdate, enddate);
            IList<Attachment> attachments = new List<Attachment>();

            if (events.Items != null && events.Items.Count > 0)
            {
                foreach (var eventItem in events.Items)
                {
                    string eventstartdate = eventItem.Start.DateTime.ToString();
                    if (String.IsNullOrEmpty(eventstartdate))
                    {
                        eventstartdate = eventItem.Start.Date;
                    }

                    string eventenddate = eventItem.End.DateTime.ToString();
                    if (String.IsNullOrEmpty(eventenddate))
                    {
                        eventenddate = eventItem.End.Date;
                    }

                    string description = (eventItem.Description.Length > 50) ? eventItem.Description.Substring(0, 50) : eventItem.Description;

                    attachments.Add(GetHeroCard(eventItem.Summary, (eventItem.Location != null) ? "At " + eventItem.Location : string.Empty, description,
                        new CardAction(ActionTypes.OpenUrl, "View Event", value: eventItem.HtmlLink)));

              }
            }

            return attachments;
        }

        private static Attachment GetHeroCard(string title, string subtitle, string text, CardAction cardAction)
        {
            var heroCard = new HeroCard
            {
                Title = title,
                Subtitle = subtitle,
                Text = text,
                Images = null,
                Buttons = new List<CardAction>() { cardAction },
            };

            return heroCard.ToAttachment();
        }

        private static string FormatDate(string datetime)
        {
            return Convert.ToDateTime(datetime).ToString("hh:mm:ss tt", CultureInfo.InvariantCulture);
        }

        private static Span FetchDate(LuisResult result)
        {
            Parser parser = new Parser();

            EntityRecommendation dateValue = new EntityRecommendation();

            if (result.TryFindEntity("builtin.datetime.date", out dateValue))
            {
                return parser.Parse(dateValue.Entity);
            }
            return null;
        }

        private static Span FetchTime(LuisResult result)
        {
            Parser parser = new Parser();

            EntityRecommendation dateValue = new EntityRecommendation();

            if (result.TryFindEntity("builtin.datetime.time", out dateValue))
            {
                return parser.Parse(dateValue.Entity);
            }
            return null;
        }

        private static string FetchString(LuisResult result, string type)
        {
            EntityRecommendation value = new EntityRecommendation();

            result.TryFindEntity(type, out value);

            return (value.Entity);
        }
    }
}