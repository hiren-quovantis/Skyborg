using Autofac;
using Microsoft.Bot.Builder.ConnectorEx;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Skyborg.Common.OAuth;
using Skyborg.Dialogs.OAuth;
using Skyborg.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Skyborg.Controllers
{
    public class GoogleAuthController : ApiController
    {
        [HttpGet]
        [Route("api/OAuthCallback")]
        public async Task<HttpResponseMessage> OAuthCallback([FromUri] string state, [FromUri] string code, CancellationToken token)
        {
            try
            {
                // Get the resumption cookie
                BotAuth botdata = JsonConvert.DeserializeObject<BotAuth>(GoogleAuthHelper.Base64Decode(state));

                var address = new Address
                    (
                        botId: GoogleAuthHelper.TokenDecoder(botdata.BotId),
                        channelId: (botdata.ChannelId),
                        userId: GoogleAuthHelper.TokenDecoder(botdata.UserId),
                        conversationId: GoogleAuthHelper.TokenDecoder(botdata.ConversationId),
                        serviceUrl: GoogleAuthHelper.TokenDecoder(botdata.ServiceUrl)
                    );

                var conversationReference = address.ToConversationReference();
                var msg = conversationReference.GetPostToUserMessage();

               // var conversationReference = new ResumptionCookie(address, address.UserId, false, "en-GB");
               // var msg = conversationReference.GetMessage();


                // ConversationReference conversationReference = JsonConvert.DeserializeObject<ConversationReference>(GoogleAuthHelper.Base64Decode(state));
                // Exchange the Auth code with Access token
                // var resumptionCookie = new ResumptionCookie(FacebookHelpers.TokenDecoder(userId), FacebookHelpers.TokenDecoder(botId), FacebookHelpers.TokenDecoder(conversationId), channelId, FacebookHelpers.TokenDecoder(serviceUrl), locale); 

                var accessToken = await GoogleAuthHelper.ExchangeCodeForAccessToken(code, GoogleAuthDialog.OauthCallback.ToString());

                //ChannelAccount botaccount = new ChannelAccount(GoogleAuthHelper.TokenDecoder(botdata.BotId));
                //ChannelAccount useraccount = new ChannelAccount(GoogleAuthHelper.TokenDecoder(botdata.UserId));
                //ConversationAccount convrersationaccount = new ConversationAccount(false, GoogleAuthHelper.TokenDecoder(botdata.ConversationId));

                //ConversationReference conversationReference = new ConversationReference(GoogleAuthHelper.TokenDecoder(botdata.ActivityId), useraccount, botaccount, convrersationaccount,
                //        (botdata.ChannelId), GoogleAuthHelper.TokenDecoder(botdata.ServiceUrl));
                // Create the message that is send to conversation to resume the login flow

                // var conversationReference = ConversationReferenceHelpers.GZipDeserialize(state);



                msg.Text = $"token:{accessToken.AccessToken}";

                // Conversation.ResumeAsync()

                // Resume the conversation
                await Conversation.ResumeAsync(conversationReference, msg, token);

                using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, msg))
                {
                    var dataBag = scope.Resolve<IBotData>();
                    await dataBag.LoadAsync(token);
                    ConversationReference pending;
                    if (dataBag.PrivateConversationData.TryGetValue("persistedCookie", out pending))
                    {
                        // remove persisted cookie
                        dataBag.PrivateConversationData.RemoveValue("persistedCookie");
                        await dataBag.FlushAsync(token);
                        return Request.CreateResponse("You are now logged in! Continue talking to the bot.");
                    }
                    else
                    {
                        // Callback is called with no pending message as a result the login flow cannot be resumed.
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, new InvalidOperationException("Cannot resume!"));
                    }
                }
            }
            catch (Exception e)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }
    }
}
