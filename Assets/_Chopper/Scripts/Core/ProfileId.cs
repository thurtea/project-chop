using UnityEngine;

namespace Chopper.Core
{
    // This PC's player profile id: made once and kept in PlayerPrefs. World saves store each player under it, so a
    // player who joins again gets their wood, gear and crypt finds back.
    public static class ProfileId
    {
        private const string Key = "Chopper.ProfileId";

        public static string Get()
        {
            string id = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(id)) return id;
            id = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(Key, id);
            PlayerPrefs.Save();
            return id;
        }
    }
}
