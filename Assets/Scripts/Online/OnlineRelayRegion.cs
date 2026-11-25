using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Relay;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>
    /// The Relay region for a new room. On the desktop it is left to Relay, which measures the closest one. A
    /// browser measures over WebTransport, which often times out (up to 10 s a region, so 15-20 s before the room
    /// exists); there the region is guessed from the browser's time zone instead, and checked against the regions
    /// Relay offers. The friend joins whatever region the room is in through the code.
    /// </summary>
    internal static class OnlineRelayRegion
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string PotionBrowserTimeZone();
#endif

        /// <summary>A region id, or null to let Relay measure.</summary>
        public static async UniTask<string> PickAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string zone = string.Empty;
            try { zone = PotionBrowserTimeZone() ?? string.Empty; }
            catch (Exception) { /* no time zone from the browser */ }
            string wanted = ForTimeZone(zone);
            try
            {
                var regions = (await RelayService.Instance.ListRegionsAsync()).Select(r => r.Id).ToList();
                if (regions.Contains(wanted)) return wanted;
                // the same part of the world, if the exact one is not offered
                string family = wanted.Split('-')[0];
                string near = regions.FirstOrDefault(id => id.StartsWith(family, StringComparison.Ordinal));
                Debug.Log($"[Online] Relay region for time zone '{zone}': {near ?? regions.FirstOrDefault()}");
                return near ?? regions.FirstOrDefault();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Online] Relay regions unavailable ({e.Message}); using {wanted}.");
                return wanted;
            }
#else
            await UniTask.CompletedTask;
            return null;
#endif
        }

        /// <summary>The Relay region nearest to an IANA time zone such as "Asia/Seoul".</summary>
        internal static string ForTimeZone(string zone)
        {
            zone ??= string.Empty;
            if (zone.StartsWith("Asia/", StringComparison.Ordinal))
            {
                if (zone is "Asia/Seoul" or "Asia/Tokyo" or "Asia/Pyongyang" or "Asia/Vladivostok") return "asia-northeast1";
                if (zone is "Asia/Kolkata" or "Asia/Calcutta" or "Asia/Karachi" or "Asia/Dhaka" or "Asia/Colombo" or "Asia/Kathmandu")
                    return "asia-south1";
                if (zone is "Asia/Dubai" or "Asia/Riyadh" or "Asia/Qatar" or "Asia/Tehran" or "Asia/Jerusalem" or "Asia/Baghdad")
                    return "europe-west4";
                return "asia-southeast1"; // China, Taiwan, Hong Kong, South-East Asia
            }
            if (zone.StartsWith("Australia/", StringComparison.Ordinal) || zone.StartsWith("Pacific/Auckland", StringComparison.Ordinal))
                return "australia-southeast1";
            if (zone.StartsWith("Europe/", StringComparison.Ordinal) || zone.StartsWith("Africa/", StringComparison.Ordinal) ||
                zone.StartsWith("Atlantic/", StringComparison.Ordinal))
                return "europe-west4";
            if (zone.StartsWith("America/", StringComparison.Ordinal))
            {
                string[] south = { "Sao_Paulo", "Argentina", "Buenos_Aires", "Santiago", "Lima", "Bogota", "Caracas", "Montevideo" };
                if (south.Any(city => zone.Contains(city))) return "southamerica-east1";
                string[] west = { "Los_Angeles", "Vancouver", "Tijuana", "Phoenix", "Denver", "Edmonton", "Boise", "Anchorage", "Juneau" };
                if (west.Any(city => zone.Contains(city))) return "us-west1";
                return "us-east1";
            }
            return "us-central1";
        }
    }
}
