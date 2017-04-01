using BestMatchDialog;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Skyborg.Dialogs
{
    [Serializable]
    public class SupportDialog : BestMatchDialog<bool>
    {
        [BestMatch(new string[] { "Hi", "Hi There", "Hello there", "Hey", "Hello",
            "Hey there", "Greetings", "Good morning", "Good afternoon", "Good evening", "Good day" },
            threshold: 0.5, ignoreCase: true, ignoreNonAlphaNumericCharacters: false)]
        public async Task HandleGreeting(IDialogContext context, string messageText)
        {
            await context.PostAsync("Well hello there. What can I do for you today? Type help|man for usage guide");
            context.Done(true);
        }

        [BestMatch("bye|bye bye|got to go|see you later|laters|adios", listDelimiter: '|', ignoreCase: true)]
        public async Task HandleGoodbye(IDialogContext context, string messageText)
        {
            await context.PostAsync("Bye. Looking forward to our next awesome conversation already.");
            context.Done(true);
        }

        [BestMatch("help|manual|support", listDelimiter: '|', ignoreCase: true)]
        public async Task HandleHelp(IDialogContext context, string messageText)
        {
            var reply = context.MakeMessage();

            reply.Attachments = GetSupportOptions();
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;

            await context.PostAsync(reply);
            context.Done(true);
        }

        public override async Task NoMatchHandler(IDialogContext context, string messageText)
        {
            context.Done(false);
        }

        private IList<Attachment> GetSupportOptions()
        {
            IList<Attachment> attachments = new List<Attachment>();

            List<CardAction> buttons = new List<CardAction>();
            buttons.Add(new CardAction(ActionTypes.ImBack, "Create new Event", value: "Create new Event"));
            buttons.Add(new CardAction(ActionTypes.ImBack, "Get today's schedule", value: "Get today's schedule"));
            buttons.Add(new CardAction(ActionTypes.ImBack, "Logout", value: "Logout"));

            var heroCard = new HeroCard
            {
                Title = "Skyborg Support",
                Subtitle = "Try the following",
                Text = "Common man",
                Images = null,
                Buttons = buttons
            };

            attachments.Add(heroCard.ToAttachment());

            return attachments;
        }
    }
}