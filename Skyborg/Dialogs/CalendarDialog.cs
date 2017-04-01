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
using Skyborg.Common;
using Skyborg.Persistance.DataStore;
using System.Threading;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Plus.v1;

namespace Skyborg.Dialogs
{
    [Serializable]
    public class CalendarDialog : IDialog<object>
    {
        string[] Scopes = {
                                CalendarService.Scope.Calendar,
                                PlusService.Scope.PlusMe
                          };

        public CalendarDialog()
        {

        }

        public async Task<UserCredential> GetGoogleCredentials(string userId, string conversationId)
        {
            EFDataStore dbstore = new EFDataStore();

            UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
            {
                ClientId = BotConstants.GoogleClientId,
                ClientSecret = BotConstants.GoogleClientSecret
            }
                                                            , Scopes
                                                            , userId
                                                            , CancellationToken.None
                                                            , dbstore).Result;
            
            PlusService service = new PlusService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Skyborg",
            });

            string googleUserId = service.People.Get("me").ExecuteAsync().Result.Id;

            await dbstore.UpdateGoogleUserIdAsync<TokenResponse>(userId, googleUserId, conversationId);
            
            return credential;
        }

        public async Task StartAsync(IDialogContext context)
        {
            await context.PostAsync("Welcome to the Google Calendar!");

            IntentModel intent;
            context.UserData.TryGetValue<IntentModel>("Intent", out intent);

            LuisResult luisresult = new LuisResult();
            luisresult.Entities = intent.Model;


            switch (intent.IntentName)
            {
                case "list":
                    await GetEventList(context, luisresult);
                    context.Wait(MessageReceivedAsync);
                    break;

                case "create":
                    var eventDetail = HydrateEventObject(luisresult);
                    var createEventDialog = FormDialog.FromForm(this.BuildCreateEventForm, FormOptions.PromptFieldsWithValues);
                    context.Call(createEventDialog, this.ResumeAfterEventDialog);
                    break;

                case "responseupdate":
                    await UpdateResponseStatus(context, luisresult);
                    context.Wait(MessageReceivedAsync);
                    break;

                default:
                    await None(context);
                    context.Done<object>(null);
                    break;
            }
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            context.Done<object>(null);
        }

        private CalendarModel HydrateEventObject(LuisResult result)
        {
            CalendarModel eventDetail = new CalendarModel();

            var startDate = FetchDate(result);

            if (startDate != null && startDate.Start.HasValue)
            {
                eventDetail.StartDate = startDate.Start.Value;
            }

            var summary = FetchString(result, "EventName");
            if (!string.IsNullOrEmpty(summary))
            {
                eventDetail.Summary = summary;
            }

            var startTime = FetchTime(result);
            if (startTime != null && startTime.Start.HasValue)
            {
                eventDetail.StartTime = startTime.Start.Value.TimeOfDay;
            }

            return eventDetail;
        }

        private async Task ResumeAfterEventDialog(IDialogContext context, IAwaitable<CalendarModel> result)
        {
            var searchQuery = await result;

            await context.PostAsync("Hope you liked my Work !!");
            context.Done<object>(null);
        }


        public async Task None(IDialogContext context)
        {
            await context.PostAsync("Sorry! I don't understand what you want");
        }

        public async Task GetEventList(IDialogContext context, LuisResult result)
        {
            //UserCredential credential;
            //context.UserData.TryGetValue<UserCredential>("Credential", out credential);
            UserCredential credential = await GetGoogleCredentials(context.Activity.From.Id, context.Activity.Conversation.Id);

            CalendarAdapter adapter = new CalendarAdapter(credential);

            var dateResult = FetchDate(result) ?? FetchTime(result);

            if (dateResult != null)
            {
                await context.PostAsync("Please wait, while I retrieve your schedule");

                IList<Attachment> attachments = this.GetEventsByDateRange(adapter, dateResult.Start.Value, dateResult.End.Value);
                if(attachments != null)
                {
                    var reply = context.MakeMessage();
                    reply.Attachments = attachments;
                    reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                    await context.PostAsync(reply);
                }

                await context.PostAsync("You don't have any event enqueued. So relax !");
            }
            else
            {
                await context.PostAsync("Sorry! I don't understand what you want");
            }


            //context.Wait(this.MessageReceivedAsync);
        }

        public async Task PushDailySchedule()
        {
            var users = new EFDataStore().GetAll<List<string>>();

            foreach (var user in users)
            {
                string[] values = user.Key.Split('|');

                await PushDailyScheduleToUser(values[1], user.ConversationId);
            }

        }

        public async Task PushDailyScheduleToUser(string userId, string conversationId)
        {
            try
            {
                CalendarAdapter adapter = new CalendarAdapter(await GetGoogleCredentials(userId, conversationId));

                var userAccount = new ChannelAccount(userId);
                var botAccount = new ChannelAccount(BotConstants.BotId);

                var connector = new ConnectorClient(new Uri("http://localhost:57509"));
                IMessageActivity message = Activity.CreateMessageActivity();
                message.From = botAccount;
                message.Recipient = userAccount;
                message.Conversation = new ConversationAccount(id: conversationId);
                
                message.Attachments = this.GetEventsByDateRange(adapter, DateTime.Now.Date, DateTime.Now.Date.AddDays(1));
                if (message.Attachments != null)
                {
                    message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                    message.Text = "Here's your today's schedule";
                }
                else
                {
                    message.Text = "You can relax for today";
                }

                await connector.Conversations.SendToConversationAsync((Activity)message);
            }
            catch(Exception e)
            {

            }
        }

        public async Task UpdateResponseStatus(IDialogContext context, LuisResult result)
        {
            CalendarAdapter adapter = new CalendarAdapter(await GetGoogleCredentials(context.Activity.From.Id, context.Activity.Conversation.Id));

            await context.PostAsync("Please wait, while I Update your response");

            if (adapter.UpdateEventConsent(FetchString(result, "eventid"), FetchString(result, "responsestatus")))
            {
                await context.PostAsync("Your event consent was update successfully");
            }
            else
            {
                await context.PostAsync("Even error occured, since this feature is still in beta.");
            }

        }


        public async Task CreateEvent(IDialogContext context, CalendarModel eventDetail)
        {

            await context.PostAsync("Creating event for you. Give me a moment ...");

            if (!string.IsNullOrEmpty(eventDetail.Summary) && eventDetail.StartDate != DateTime.MinValue)
            {

                CalendarAdapter adapter = new CalendarAdapter(await GetGoogleCredentials(context.Activity.From.Id, context.Activity.Conversation.Id));

                string creatorId = (new EFDataStore()).GetById<TokenResponse>(context.Activity.From.Id).GoogleUserId;

                Event calendarEvent = adapter.CreateEvent(eventDetail, creatorId);

                var reply = context.MakeMessage();

                reply.Summary = "Event Created Successfully";
                reply.Text = "Access it here: " + calendarEvent.HtmlLink;

                await context.PostAsync(reply);
            }
            else
            {
                await context.PostAsync("Please provide valid summary, starttime and date");
            }

            //context.Wait(this.MessageReceivedAsync);
        }

        public IForm<CalendarModel> BuildCreateEventForm()
        {
            OnCompletionAsyncDelegate<CalendarModel> createEventtemp = async (context, state) =>
            {
                await context.PostAsync("Creating events ...");
                await CreateEvent(context, state);
            };

            return new FormBuilder<CalendarModel>()
                        .Message("Creating Event for You . . .")
                        .AddRemainingFields()
                        .Confirm("Do you want to create event {Summary} at {Location} for {StartDate} {StartTime} with {Attendees}?")
                        .OnCompletion(createEventtemp)
                        .Build();
        }

        #region "Private Helper"

        private IList<Attachment> GetEventsByDateRange(CalendarAdapter adapter, DateTime startdate, DateTime enddate)
        {
            string response = string.Empty;

            Events events = adapter.GetEventsByDateRange(startdate, enddate);
            IList<Attachment> attachments = null;

            if (events.Items != null && events.Items.Count > 0)
            {
                attachments = new List<Attachment>();

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

                    string description = string.Empty;
                    if (!string.IsNullOrEmpty(description))
                    {
                        description = (eventItem.Description.Length > 50) ? eventItem.Description.Substring(0, 50) : eventItem.Description;
                    }

                    string responseStatus = string.Empty;

                    attachments.Add(GetHeroCard(eventItem.Summary, (eventItem.Location != null) ? "At " + eventItem.Location : string.Empty, eventItem.Attendees, eventItem.Id));

                }
            }

            return attachments;
        }

        private static Attachment GetHeroCard(string title, string subtitle, IList<EventAttendee> attendees, string eventId)
        {
            string responseStatus = string.Empty;
            List<CardAction> buttons = new List<CardAction>();

            if (attendees.FirstOrDefault(a => a.Self == true) != null)
            {
                responseStatus = attendees.First(a => a.Self == true).ResponseStatus;

                switch (responseStatus)
                {
                    case "needsAction":
                        responseStatus = "You have not responded yet";
                        buttons.Add(new CardAction(ActionTypes.ImBack, "Yes", value: string.Format("EventResponse {0} accepted", eventId)));
                        buttons.Add(new CardAction(ActionTypes.ImBack, "No", value: string.Format("EventResponse {0} declined", eventId)));
                        buttons.Add(new CardAction(ActionTypes.ImBack, "Tentative", value: string.Format("EventResponse {0} tentative", eventId)));
                        break;
                    case "declined":
                        responseStatus = "You have declined";
                        buttons.Add(new CardAction(ActionTypes.ImBack, "Yes", value: string.Format("EventResponse {0} accepted", eventId)));
                        buttons.Add(new CardAction(ActionTypes.ImBack, "Tentative", value: string.Format("EventResponse {0} tentative", eventId)));
                        break;
                    case "tentative":
                        responseStatus = "You have tentatively accepted";
                        buttons.Add(new CardAction(ActionTypes.ImBack, "Yes", value: string.Format("EventResponse {0} accepted", eventId)));
                        buttons.Add(new CardAction(ActionTypes.ImBack, "No", value: string.Format("EventResponse {0} declined", eventId)));
                        break;
                    case "accepted":
                        responseStatus = "You have accepted";
                        buttons.Add(new CardAction(ActionTypes.ImBack, "No", value: string.Format("EventResponse {0} declined", eventId)));
                        buttons.Add(new CardAction(ActionTypes.ImBack, "Tentative", value: string.Format("EventResponse {0} tentative", eventId)));
                        break;
                    default:
                        break;
                }
            }
            else
            {
                responseStatus = "You weren't invited";
            }

            var heroCard = new HeroCard
            {
                Title = title,
                Subtitle = subtitle,
                Text = responseStatus,
                Images = null,
                Buttons = buttons
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

            if (value != null)
            {
                return (value.Entity);
            }
            return string.Empty;
        }

        private static int FetchInteger(LuisResult result, string type)
        {
            EntityRecommendation value = new EntityRecommendation();

            result.TryFindEntity(type, out value);

            if (value != null)
            {
                return Convert.ToInt32(value.Entity);
            }
            return 0;
        }

        #endregion
    }
}