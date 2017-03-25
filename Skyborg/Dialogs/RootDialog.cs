using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Microsoft.Bot.Builder.Dialogs;
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
        //[NonSerialized]
        //IntentModel intent = null;

        //String SenderId = string.Empty;
        
        //public RootDialog(IntentModel intent, string senderId)
        //{
        //    this.intent = intent;
        //    this.SenderId = senderId;
        //}

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            //var reply = context.MakeMessage();

            //await context.PostAsync(reply);

            var message = await result;
            LUISAdaptor adaptor = new LUISAdaptor();
            IntentModel intent = await adaptor.Execute(message.Text);

            if (intent != null && !string.IsNullOrEmpty(intent.ClassName))
            {
                context.UserData.SetValue<IntentModel>("Intent", intent);

                switch (intent.ClassName)
                {
                    case "calendar":
                        await context.Forward(new CalendarDialog(), this.ResumeAfterOptionDialog, message, CancellationToken.None);
                        //context.Call(new CalendarDialog(), this.ResumeAfterOptionDialog);
                        break;
                    default:
                        context.Wait(this.MessageReceivedAsync);
                        break;
                }
            }
            else
            {
                await context.PostAsync("I'm not sure if I understand what you mean !!");
                context.Wait(this.MessageReceivedAsync);
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
            await context.PostAsync("Thanks for using our Calendar Services.");
            context.Wait(this.MessageReceivedAsync);
        }
    }
}