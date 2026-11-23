using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SPT.Common.Http;

namespace Nicotine.Client
{
    internal static class Backend
    {
        public static NicotineConfig Config { get; private set; } = new NicotineConfig();

        public static HashSet<string> PackIds { get; private set; } = new HashSet<string>(new NicotineConfig().Packs.Ids);

        private static readonly JsonSerializerSettings Read = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        public static async Task<SavedState> LoadAsync()
        {
            var json = await RequestHandler.GetJsonAsync("/nicotine/state");
            var response = JsonConvert.DeserializeObject<StateResponse>(json, Read);
            if (response?.Config != null)
            {
                Config = response.Config;
                PackIds = new HashSet<string>(response.Config.Packs?.Ids ?? new List<string>());
            }

            return response?.State ?? new SavedState();
        }

        public static async Task SaveAsync(SavedState state)
        {
            try
            {
                await RequestHandler.PostJsonAsync("/nicotine/save", JsonConvert.SerializeObject(state));
            }
            catch (Exception ex)
            {
                NicotinePlugin.Log.LogWarning($"Nicotine could not save addiction state: {ex.Message}");
            }
        }
    }
}
