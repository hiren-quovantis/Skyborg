using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using System.Text.RegularExpressions;
using Skyborg.Common.OAuth;
using Microsoft.Bot.Builder.ConnectorEx;

namespace Skyborg.Dialogs.OAuth
{
    [Serializable]
    public class GoogleAuthDialog : IDialog<string>
    {
        public static readonly Uri OauthCallback = new Uri("http://localhost:3979/api/OAuthCallback");

        /// <summary>
        /// The key that is used to keep the AccessToken in <see cref="Microsoft.Bot.Builder.Dialogs.Internals.IBotData.PrivateConversationData"/>
        /// </summary>
        public static readonly string AuthTokenKey = "AuthToken";

        public static readonly IDialog<string> dialog = Chain
            .PostToChain()
            .Switch(
                new Case<IMessageActivity, IDialog<string>>((msg) =>
                {
                    var regex = new Regex("^login", RegexOptions.IgnoreCase);
                    return regex.IsMatch(msg.Text);
                }, (ctx, msg) =>
                {
                    // User wants to login, send the message to Google Auth Dialog
                    return Chain.ContinueWith(new GoogleAuthDialog(),
                                async (context, res) =>
                                {
                                    // The Google Auth Dialog completed successfully and returend the access token in its results
                                    var token = await res;
                                    var valid = await GoogleAuthHelper.ValidateAccessToken(token);
                                    var name = await GoogleAuthHelper.GetFacebookProfileName(token);
                                    context.UserData.SetValue("name", name);
                                    return Chain.Return($"Your are logged in as: {name}");
                                });
                }),
                new Case<IMessageActivity, IDialog<string>>((msg) =>
                {
                    var regex = new Regex("^logout", RegexOptions.IgnoreCase);
                    return regex.IsMatch(msg.Text);
                }, (ctx, msg) =>
                {
                    // Clearing user related data upon logout
                    ctx.PrivateConversationData.RemoveValue(AuthTokenKey);
                    ctx.UserData.RemoveValue("name");
                    return Chain.Return($"Your are logged out!");
                }),
                new DefaultCase<IMessageActivity, IDialog<string>>((ctx, msg) =>
                {
                    string token;
                    string name = string.Empty;
                    if (ctx.PrivateConversationData.TryGetValue(AuthTokenKey, out token))// && ctx.UserData.TryGetValue("name", out name))
                    {
                        var isValid = GoogleAuthHelper.ValidateAccessToken(token);
                        isValid.Wait();

                        if (isValid.IsCompleted && isValid.Result)
                        {
                            return Chain.Return($"Your are logged in as: Hiren");
                        }
                        else
                        {
                            return Chain.Return($"Your Token has expired! Say \"login\" to log you back in!");
                        }
                    }
                    else
                    {
                        return Chain.Return("Say \"login\" when you want to login to Google!");
                    }
                })
            ).Unwrap().PostToUser();


        public GoogleAuthDialog()
        {

        }

        public async Task StartAsync(IDialogContext context)
        {
            await LogIn(context);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var msg = await (argument);
            if (msg.Text.StartsWith("token:"))
            {
                try
                {
                    // Dialog is resumed by the OAuth callback and access token
                    // is encoded in the message.Text
                    var token = msg.Text.Remove(0, "token:".Length);
                    context.PrivateConversationData.SetValue(AuthTokenKey, token);
                    context.Wait(this.MessageReceivedAsync);
                }
                catch (Exception e)
                {

                }

               // context.Done<object>(null);
                }
            else
            {
                await LogIn(context);
            }
        }

        /// <summary>
        /// Login the user.
        /// </summary>
        /// <param name="context"> The Dialog context.</param>
        /// <returns> A task that represents the login action.</returns>
        private async Task LogIn(IDialogContext context)
        {
            string token;
            if (!context.PrivateConversationData.TryGetValue(AuthTokenKey, out token))
            {
                var conversationReference = context.Activity.ToConversationReference();
                conversationReference.ActivityId = context.Activity.Id;

                context.PrivateConversationData.SetValue("persistedCookie", conversationReference);

                // sending the sigin card with Google login url
                var reply = context.MakeMessage();
                var googleLoginUrl = GoogleAuthHelper.GetGoogleLoginURL(conversationReference, OauthCallback.ToString());
                reply.Text = "Please login in using this card";
                reply.Attachments.Add(SigninCard.Create("You need to authorize me",
                                                        "Login to Google!",
                                                        googleLoginUrl
                                                        ).ToAttachment());
                await context.PostAsync(reply);
                context.Wait(MessageReceivedAsync);
            }
            else
            {
                context.Done(token);
            }
        }
    }
}