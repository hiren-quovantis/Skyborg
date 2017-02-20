using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Skyborg.Common;
using Skyborg.Model;
using System.Threading.Tasks;

namespace Skyborg.Adapters.NLP
{
    public class LUISAdaptor
    {
        LuisModelAttribute attribute;
        LuisService service;

        public LUISAdaptor()
        {
            attribute = new LuisModelAttribute(BotConstants.LUISModelId, BotConstants.LUISSubscriptionKey);
            service = new LuisService(attribute);
        }

        public async Task<IntentModel> Execute(string query)
        {
            IntentModel intent = null;
            
            LuisResult result = await service.QueryAsync(query);
            
            if(result.Intents != null && result.Intents.Count > 0)
            {
                if(result.Intents[0].Score.Value > 0.7)
                {
                    intent = new IntentModel(result);
                }
            }
            return intent;
        }

    }
}