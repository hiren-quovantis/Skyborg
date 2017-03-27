using Microsoft.Bot.Builder.Luis.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Skyborg.Model
{
    [Serializable]
    public class IntentModel //where T : object
    {
        public IntentModel()
        {
        }

        public IntentModel(LuisResult result)
        {
            this.OriginalIntent = result.Intents[0].Intent;
            this.Model = result.Entities;
        }

        public IntentModel(string classname, string intentname)
        {
            this.ClassName = classname;
            this.IntentName = intentname;
        }

        private string OriginalIntent
        {
            set
            {
                string[] values = value.Split('.');
                if (values.Length == 3)
                {
                    this.ClassName = values[0];
                    this.IntentName = values[2];
                }
                else
                {
                    this.ClassName = values[0];
                    this.IntentName = "none";
                }
            }
        }

        public string ClassName { get; set; }

        public string IntentName { get; set; }
        
        
        public IList<EntityRecommendation> Model
        {
            get;
            set;
        }
        
    }
}