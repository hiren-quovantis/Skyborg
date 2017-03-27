﻿using Microsoft.Bot.Builder.Dialogs;
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

namespace Skyborg.Dialogs
{
    [Serializable]
    public class CalendarDialog : IDialog<object>
    {
        string[] Scopes = { CalendarService.Scope.Calendar };

        // UserCredential credential;



        public CalendarDialog()
        {

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
            UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
            {
                ClientId = BotConstants.GoogleClientId,
                ClientSecret = BotConstants.GoogleClientSecret
            }
                                                            , Scopes
                                                            , context.Activity.From.Id
                                                            , CancellationToken.None
                                                            , new EFDataStore()).Result;

            CalendarAdapter adapter = new CalendarAdapter(credential);

            var dateResult = FetchDate(result) ?? FetchTime(result);

            if (dateResult != null)
            {
                await context.PostAsync("Please wait, while I retrieve your schedule");

                var reply = context.MakeMessage();

                reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                reply.Attachments = this.GetEventsByDateRange(adapter, dateResult.Start.Value, dateResult.End.Value);

                await context.PostAsync(reply);
            }
            else
            {
                await context.PostAsync("Sorry! I don't understand what you want");
            }


            //context.Wait(this.MessageReceivedAsync);
        }

        public async Task UpdateResponseStatus(IDialogContext context, LuisResult result)
        {
            //UserCredential credential;
            //context.UserData.TryGetValue<UserCredential>("Credential", out credential);
            UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
            {
                ClientId = BotConstants.GoogleClientId,
                ClientSecret = BotConstants.GoogleClientSecret
            }
                                                            , Scopes
                                                            , context.Activity.From.Id
                                                            , CancellationToken.None
                                                            , new EFDataStore()).Result;

            CalendarAdapter adapter = new CalendarAdapter(credential);

            await context.PostAsync("Please wait, while I Update your response");

            if(adapter.UpdateEventConsent(FetchString(result, "eventid"), FetchString(result, "responsestatus")))
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
            UserCredential credential;

            await context.PostAsync("Allow me to create an event for you (y)");

            if (!string.IsNullOrEmpty(eventDetail.Summary) && eventDetail.StartDate != DateTime.MinValue)
            {

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
                {
                    ClientId = BotConstants.GoogleClientId,
                    ClientSecret = BotConstants.GoogleClientSecret
                }
                                                            , Scopes
                                                            , context.Activity.From.Id
                                                            , CancellationToken.None
                                                            , new EFDataStore()).Result;

                CalendarAdapter adapter = new CalendarAdapter(credential);
                Event calendarEvent = adapter.CreateEvent(eventDetail);

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
                        .OnCompletion(createEventtemp)
                        .Build();
        }

        #region "Private Helper"

        private IList<Attachment> GetEventsByDateRange(CalendarAdapter adapter, DateTime startdate, DateTime enddate)
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
                        buttons.Add(new CardAction(ActionTypes.PostBack, "Yes", value: string.Format("EventResponse {0} accepted", eventId)));
                        buttons.Add(new CardAction(ActionTypes.PostBack, "No", value: string.Format("EventResponse {0} declined", eventId)));
                        buttons.Add(new CardAction(ActionTypes.PostBack, "Tentative", value: string.Format("EventResponse {0} tentative", eventId)));
                        break;
                    case "declined":
                        responseStatus = "You have declined";
                        buttons.Add(new CardAction(ActionTypes.PostBack, "Yes", value: string.Format("EventResponse {0} accepted", eventId)));
                        buttons.Add(new CardAction(ActionTypes.PostBack, "Tentative", value: string.Format("EventResponse {0} tentative", eventId)));
                        break;
                    case "tentative":
                        responseStatus = "You have tentatively accepted";
                        buttons.Add(new CardAction(ActionTypes.PostBack, "Yes", value: string.Format("EventResponse {0} accepted", eventId)));
                        buttons.Add(new CardAction(ActionTypes.PostBack, "No", value: string.Format("EventResponse {0} declined", eventId)));
                        break;
                    case "accepted":
                        responseStatus = "You have accepted";
                        buttons.Add(new CardAction(ActionTypes.PostBack, "No", value: string.Format("EventResponse {0} declined", eventId)));
                        buttons.Add(new CardAction(ActionTypes.PostBack, "Tentative", value: string.Format("EventResponse {0} tentative", eventId)));
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