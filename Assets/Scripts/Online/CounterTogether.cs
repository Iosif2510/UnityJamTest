using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>
    /// The counter's online moments, kept in one place for <see cref="OrderSceneView"/>: the storeroom stays shut
    /// until the friend upstairs has found the memo, and the hand-over and the result card start on both screens at
    /// once (<see cref="PairReady"/>).
    /// </summary>
    internal static class CounterTogether
    {
        private static OnlineSession Live =>
            OnlineSession.Exists && OnlineSession.Instance.PeerConnected ? OnlineSession.Instance : null;

        /// <summary>Whether a friend is on the line and sees this counter moment too (the maker, or a watched hand-over).</summary>
        internal static bool FriendShares(bool watching)
        {
            var session = Live;
            return session != null && (session.Role == OnlineRole.Maker || watching);
        }

        /// <summary>The maker downstairs, before the friend upstairs pressed "찾았어!" for this customer.</summary>
        internal static bool MemoStillHidden(float customerSince)
        {
            var session = Live;
            return session != null && session.Role == OnlineRole.Maker && session.Signal != null &&
                session.Signal.ReceivedAt < customerSince;
        }

        /// <summary>
        /// Says this player is ready for <paramref name="topic"/> and waits until both are (true), the friend leaves,
        /// or <paramref name="timeoutSeconds"/> pass (false; 0 waits as long as the friend stays).
        /// </summary>
        internal static async UniTask<bool> Agree(PairTopic topic, float timeoutSeconds, CancellationToken token)
        {
            var session = Live;
            if (session == null) return false;
            var pairs = session.Pairs;
            bool went = false;
            void OnWent(PairTopic t, byte[] extra)
            {
                if (t == topic) went = true;
            }
            pairs.Went += OnWent;
            try
            {
                pairs.SetReady(topic);
                float since = Time.unscaledTime;
                while (!went && Live != null && (timeoutSeconds <= 0f || Time.unscaledTime - since < timeoutSeconds))
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                // not together after all: take the ready mark back so it does not count for the next time
                if (!went && OnlineSession.Exists) OnlineSession.Instance.Pairs.SetReady(topic, false);
                return went;
            }
            finally
            {
                pairs.Went -= OnWent;
            }
        }
    }
}
