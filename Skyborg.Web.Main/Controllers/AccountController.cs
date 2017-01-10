using Skyborg.Model;
using Skyborg.Persistance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Google.Apis.Oauth2.v2;
using Google.Apis.Oauth2.v2.Data;
using Google.Apis.Auth.OAuth2;
using System.IO;
using System.Threading;
using Google.Apis.Util.Store;
using Google.Apis.Calendar.v3;
using Google.Apis.Auth.OAuth2.Flows;
using System.Collections.Specialized;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Skyborg.Web.Main.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        public ActionResult Index()
        {
            return View();
        }

        public void Authenticate(string userId, string name)
        {
            /*
            string[] Scopes = { CalendarService.Scope.CalendarReadonly };

            UserCredential credential;

            using (var stream =
                new FileStream(AppDomain.CurrentDomain.BaseDirectory + "/client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/calendar-dotnet-quickstart.json");

                //GoogleAuthorizationCodeFlow flow = new GoogleAuthorizationCodeFlow()  

               // GoogleAuthorizationCodeFlow.Initializer init = new GoogleAuthorizationCodeFlow.Initializer();
                //init.

                // GoogleWebAuthorizationBroker.AuthorizeAsync()

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;

                credential.
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            */

            
            Session["UserId"] = userId;
            Session["Name"] = name;

            Response.Redirect("https://accounts.google.com/o/oauth2/auth?" 
                + "access_type=offline&"
                + "client_id=551969187605-irq883ar9v89e5dtj66s17rruo8t6358.apps.googleusercontent.com&"
                + "scope=https://www.googleapis.com/auth/calendar&"
                + "response_type=code&"
                + "approval_prompt=force&"
                + "redirect_uri=http://localhost:25738/Account/OnAuthSuccess");

    
        }

        [HttpGet, Route("Account/OnAuthSuccess/{code}")]
        public ActionResult OnAuthSuccess(string code)
        {
            NameValueCollection param = new NameValueCollection();
            param.Add("client_id", "551969187605-irq883ar9v89e5dtj66s17rruo8t6358.apps.googleusercontent.com");
            param.Add("client_secret", "SZBJWYWjiCssbnQ2ZxCptPj4");
            param.Add("redirect_uri", "http://localhost:25738/Account/OnAuthSuccess");
            param.Add("grant_type", "authorization_code");
            param.Add("code", code);

            var response = JObject.Parse(this.Post("https://www.googleapis.com/oauth2/v4/token", param));

            
            User user = new User();
            user.GoogleRefreshToken = response["refresh_token"].ToObject<string>();
            user.Name = Session["UserId"].ToString();
            user.EmailId = Session["Name"].ToString();

            UserRepository userRepository = new UserRepository();
            userRepository.Upsert(user);

            return View();
        }

        private string Post(string uri, NameValueCollection pairs)
        {
            byte[] response = null;
            using (WebClient client = new WebClient())
            {
                response = client.UploadValues(uri, pairs);
            }
            return response.ToString();
        }


    }
}