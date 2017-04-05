﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using Skyborg.Adapters.NLP;
using Skyborg.Common;
using Skyborg.Model;
using Skyborg.Persistance.DataStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Skyborg.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            //var reply = context.MakeMessage();

            //await context.PostAsync(reply);

            var message = await result;
            IntentModel intent;

            if (message.Text.Contains("EventResponse"))
            {
                string[] entity = message.Text.Split(' ');
                intent = new IntentModel("calendar", "responseupdate");
                intent.Model = new List<EntityRecommendation>();
                intent.Model.Add(new EntityRecommendation("eventid", null, entity[1]));
                intent.Model.Add(new EntityRecommendation("responsestatus", null, entity[2]));
            }
            else
            {
                LUISAdaptor adaptor = new LUISAdaptor();
                intent = await adaptor.Execute(message.Text);
            }

            try
            {
                if (intent != null && !string.IsNullOrEmpty(intent.ClassName))
                {
                    context.UserData.SetValue<IntentModel>("Intent", intent);

                    switch (intent.ClassName)
                    {
                        case "calendar":
                            await context.Forward(new CalendarDialog(), this.ResumeAfterCalendarDialog, message, CancellationToken.None);
                           // context.Call(new CalendarDialog(), this.ResumeAfterCalendarDialog);
                            break;
                        default:
                            context.Wait(this.MessageReceivedAsync);
                            break;
                    }
                }
                else
                {
                    var dialog = new SupportDialog();
                    dialog.InitialMessage = message.Text;
                    context.Call(dialog, AfterCommonResponseHandled);
                }
            }
            catch (Exception ex)
            {
                await context.PostAsync($"Ooops! something went wrong :(. But don't worry, I'm handling that exception and you can try again!" + ex.Message + ex.StackTrace);
                

                context.Wait(this.MessageReceivedAsync);
            }
            //context.Wait(this.MessageReceivedAsync);
        }

        private async Task AfterCommonResponseHandled(IDialogContext context, IAwaitable<bool> result)
        {
            var messageHandled = await result;

            if (!messageHandled)
            {
                await context.PostAsync("I’m not sure what you want");
            }

            context.Wait(MessageReceivedAsync);
        }

        private async Task ResumeAfterCalendarDialog(IDialogContext context, IAwaitable<object> result)
        {
            var r = await result; 

            await context.PostAsync("Thanks for using our Calendar Services.");
           // context.Wait(this.MessageReceivedAsync);
        }
    }
}