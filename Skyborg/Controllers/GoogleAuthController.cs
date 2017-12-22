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
                
                var accessToken = await GoogleAuthHelper.ExchangeCodeForAccessToken(code, GoogleAuthDialog.OauthCallback.ToString());
                
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
