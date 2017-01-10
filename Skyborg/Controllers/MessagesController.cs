using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Bot.Builder.Dialogs;
using Skyborg.Dialogs;
using Skyborg.Persistance;
using Skyborg.Model;
using Google.Apis.Auth.OAuth2;
using System.Threading;
using Skyborg.Persistance.DataStore;
using Google.Apis.Calendar.v3;

namespace Skyborg
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            string[] scopes = { CalendarService.Scope.Calendar };
            
            if (activity.Type == ActivityTypes.Message)
            {
                // await Conversation.SendAsync(activity, () => new CalendarDialog(user));   

                //GoogleWebAuthorizationBroker.AuthorizeAsync()

                UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets { ClientId = "551969187605-r8h6f3pmge84rp8h81ud9v7pf7kn29sv.apps.googleusercontent.com",
                                                                                            ClientSecret = "NZqX0IhmKuxuQGYE72K0UBN1" }
                                                                                             , scopes
                                                                                             , activity.From.Id
                                                                                             , CancellationToken.None
                                                                                             , new EFDataStore()).Result;

                await Conversation.SendAsync(activity, () => new CalendarDialog(credential));
            }
            else
            {
                HandleSystemMessage(activity);
            }

            /*
            // AUTHN and AUTHZ CODE
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));

            // var reply = await connector.Conversations.SendToConversationAsync(Authenticate(activity));
            
            Conversation.SendAsync(activity, () => Authenticate(activity));
            */
            return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
            
        }

        private Activity Authenticate([FromBody]Activity activity)
        {
            Activity replyToConversation = activity.CreateReply("Should go to conversation, sign-in card");
            replyToConversation.Recipient = activity.From;
            replyToConversation.Type = "message";

            replyToConversation.Attachments = new List<Attachment>();
            List<CardAction> cardButtons = new List<CardAction>();
            CardAction plButton = new CardAction()
            {
                Value = "http://localhost:25738/Account/Authenticate?userId=" + activity.From.Id + "&name=" + activity.From.Name,
                Type = "signin",
                Title = "Connect"
            };
            cardButtons.Add(plButton);

            SigninCard plCard = new SigninCard("You need to authorize me", cardButtons);

            Attachment plAttachment = plCard.ToAttachment();
            replyToConversation.Attachments.Add(plAttachment);

            return replyToConversation;

        }


        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
}