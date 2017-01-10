using Microsoft.Bot.Builder.Dialogs;
using System;
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
            Chronic.Parser parser = new Chronic.Parser();
            EntityRecommendation dateValue = new EntityRecommendation();

            var entities = new List<EntityRecommendation>(result.Entities);

            result.TryFindEntity("builtin.datetime.date", out dateValue);

            var dateResult = parser.Parse(dateValue.Entity);

            if (dateResult != null)
            {
                var reply = context.MakeMessage();

                reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                reply.Attachments = this.GetEventsByDateRange(dateResult.Start.Value, dateResult.End.Value);

                await context.PostAsync(reply);

                context.Wait(this.MessageReceived);
            }


            /*
        if(entities.Any(entity => entity.Type == "builtin.datetime.date"))
        {
            var dateEntity = entities.Where(entity => entity.Type == "builtin.datetime.date").First();

            var date = dateEntity.Resolution.First().Value ?? null;

            if(!string.IsNullOrEmpty(date))
            {
                var dateValue = DateTime.MinValue;

                Chronic.Parser parser = new Chronic.Parser();
                DateTime.TryParse(date, out dateValue);

                if(dateValue != DateTime.MinValue)
                {
                    await context.PostAsync(this.GetEventsByDate(dateValue));
                }
            }
        }
        */

            context.Wait(MessageReceived);
        }

        [LuisIntent("Create Event")]
        public async Task CreateEvent(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Event created successfully");

            context.Wait(this.MessageReceived);
        }

        /*
        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var message = context.MakeMessage();

            message.Text = this.GetTodaysEvent();

            await context.PostAsync(message);

            context.Wait(this.MessageReceivedAsync);
        }

    */
        //private Attachment GetSigninCard()
        //{

        //    var signinCard = new SigninCard
        //    {
        //        Text = "Authenticate yourself on Google First!",
        //        Buttons = new List<CardAction> {
        //            new CardAction(ActionTypes.Signin, "Sign-in",
        //                value: "http://localhost:25738/Account/Authenticate?userId=" + this.user.Name  + "&name=" + this.user.EmailId) }
        //    };

        //    return signinCard.ToAttachment();
        //}

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


                    //response += string.Format("{0} at ({1} - {2}) {3} ", eventItem.Summary, FormatDate(eventstartdate), FormatDate(eventenddate), Environment.NewLine);
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
    }
}