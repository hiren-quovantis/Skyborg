using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
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
    //[Serializable]
    public class RootDialog : IDialog<object>
    {
        //[NonSerialized]
        IntentModel intent = null;

        String SenderId = string.Empty;

        string[] Scopes = { CalendarService.Scope.Calendar };

        public RootDialog(IntentModel intent, string senderId)
        {
            this.intent = intent;
            this.SenderId = senderId;
        }

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var reply = context.MakeMessage();

            await context.PostAsync(reply);

            var message = await result;

            UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
            {
                ClientId = BotConstants.GoogleClientId,
                ClientSecret = BotConstants.GoogleClientSecret
            }
                                                            , Scopes
                                                            , SenderId
                                                            , CancellationToken.None
                                                            , new EFDataStore()).Result;

            switch (this.intent.ClassName)
            {
                case "calendar":
                     await context.Forward(new CalendarDialog(credential, this.intent), this.ResumeAfterOptionDialog, message, CancellationToken.None);
                    //context.Call(new CalendarDialog(credential, this.intent), this.ResumeAfterOptionDialog);
                    break;
                default:
                    context.Wait(this.MessageReceivedAsync);
                    break;
            }
            //context.Wait(this.MessageReceivedAsync);
        }

        private async Task ResumeAfterOptionDialog(IDialogContext context, IAwaitable<object> result)
        {
            //try
            //{
            //    var message = await result;
            //}
            //catch (Exception ex)
            //{
            //    await context.PostAsync($"Error: {ex.Message}");
            //}
            context.Wait(this.MessageReceivedAsync);
        }
    }
}