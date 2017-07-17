using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Skyborg.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder.ConnectorEx;
using System.Web.Helpers;

namespace Skyborg.Common.OAuth
{
    public class AcessToken
    {
        public AcessToken()
        {
        }

        [JsonProperty(PropertyName = "access_token")]
        public string AccessToken { get; set; }

        [JsonProperty(PropertyName = "token_type")]
        public string TokenType { get; set; }

        [JsonProperty(PropertyName = "expires_in")]
        public long ExpiresIn { get; set; }
    }

    class GoogleProfile
    {
        public GoogleProfile()
        {
        }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "email")]
        public string EMail { get; set; }
    }

    public class GoogleAuthHelper
    {
        static string[] scopes =
            {
                "https://www.googleapis.com/auth/calendar",
                "https://www.googleapis.com/auth/userinfo.email",
                "https://www.googleapis.com/auth/userinfo.profile"
            };



        public async static Task<AcessToken> ExchangeCodeForAccessToken(string code, string googleOauthCallback)
        {
            var uri = GetUri("https://www.googleapis.com/oauth2/v4/token");
            var content = new FormUrlEncodedContent(new[]
                           {
                                new KeyValuePair<string, string>("client_id", BotConstants.GoogleClientId),
                                new KeyValuePair<string, string>("client_secret", BotConstants.GoogleClientSecret),
                                 new KeyValuePair<string, string>("redirect_uri", googleOauthCallback),
                                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                                 new KeyValuePair<string, string>("code", code)
                            });


            return await GooglePostRequest<AcessToken>(uri, content);
        }

        public static async Task<string> ValidateAccessToken(string accessToken)
        {
            var uri = GetUri("https://www.googleapis.com/oauth2/v3/tokeninfo",
                Tuple.Create("access_token", accessToken));

            var res = await GoogleRequest<GoogleProfile>(uri).ConfigureAwait(false);
            return res.EMail;
        }
        
        private static string GetOAuthCallBack(ConversationReference conversationReference, string googleOauthCallback)
        {
            //conversationReference.
            BotAuth botdata = new BotAuth();
            botdata.UserId = TokenEncoder(conversationReference.User.Id);
            botdata.BotId = TokenEncoder(conversationReference.Bot.Id);
            botdata.ConversationId = TokenEncoder(conversationReference.Conversation.Id);
            botdata.ServiceUrl = TokenEncoder(conversationReference.ServiceUrl);
            botdata.ChannelId = (conversationReference.ChannelId);
            botdata.ActivityId = TokenEncoder(conversationReference.ActivityId);


            return Base64Encode(JsonConvert.SerializeObject(botdata));

            //return Base64Encode(JsonConvert.SerializeObject(conversationReference));
            //try
            //{
            //    return conversationReference.GZipSerialize();
            //}
            //catch (Exception e)
            //{
            //    return string.Empty;
            //}
            //finally
            //{

            //}
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        // because of a limitation on the characters in Facebook redirect_uri, we don't use the serialization of the cookie.
        // http://stackoverflow.com/questions/4386691/facebook-error-error-validating-verification-code
        public static string TokenEncoder(string token)
        {
            return HttpServerUtility.UrlTokenEncode(Encoding.UTF8.GetBytes(token));
        }

        public static string TokenDecoder(string token)
        {
            return Encoding.UTF8.GetString(HttpServerUtility.UrlTokenDecode(token));
        }

        public static string GetGoogleLoginURL(ConversationReference conversationReference, string OauthCallback)
        {
            var state = GetOAuthCallBack(conversationReference, OauthCallback);
            var uri = GetUri("https://accounts.google.com/o/oauth2/v2/auth ",
                 Tuple.Create("access_type", "offline"),
                 Tuple.Create("response_type", "code"),
                 Tuple.Create("approval_prompt", "force"),
                 Tuple.Create("client_id", BotConstants.GoogleClientId),
                 Tuple.Create("redirect_uri", OauthCallback),
                 Tuple.Create("state", state),
                 Tuple.Create("scope", String.Join(" ", scopes))
                 );

            return uri.ToString();
        }

        private static async Task<T> GoogleRequest<T>(Uri uri)
        {
            string json;
            using (HttpClient client = new HttpClient())
            {
                json = await client.GetStringAsync(uri).ConfigureAwait(false);
            }

            try
            {
                var result = JsonConvert.DeserializeObject<T>(json);
                return result;
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Unable to deserialize the Google response.", ex);
            }
        }

        private static async Task<T> GooglePostRequest<T>(Uri uri, FormUrlEncodedContent content)
        {
            string json;
            using (HttpClient client = new HttpClient())
            {
                var result = await client.PostAsync(uri, content);
                json = await result.Content.ReadAsStringAsync();
            }

            try
            {
                var result = JsonConvert.DeserializeObject<T>(json);
                return result;
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Unable to deserialize the Google response.", ex);
            }
        }

        private static Uri GetUri(string endPoint, params Tuple<string, string>[] queryParams)
        {
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            foreach (var queryparam in queryParams)
            {
                queryString[queryparam.Item1] = queryparam.Item2;
            }

            var builder = new UriBuilder(endPoint);
            builder.Query = queryString.ToString();
            return builder.Uri;
        }

    }
}
