using System;
using Cysharp.Threading.Tasks;
using GameSDK;

namespace GameLogic.Maze
{
    public interface IMazeRewardedVideo { UniTask<bool> ShowAsync(string purpose); }
    [Serializable]
    public sealed class MazeAdConfig { public string hintAdId = ""; public string skipAdId = ""; }

    /// <summary>Uses the existing platform SDK, without old-game currency, UI or automatic-success assumptions.</summary>
    public sealed class MazeRewardedVideo : IMazeRewardedVideo
    {
        private readonly MazeAdConfig _config;
        public MazeRewardedVideo(MazeAdConfig config) { _config = config ?? new MazeAdConfig(); }
        public async UniTask<bool> ShowAsync(string purpose)
        {
            string id = purpose == "hint" ? _config.hintAdId : _config.skipAdId;
            if (string.IsNullOrWhiteSpace(id)) return false;
            IRewardedVideoAd ad = null;
            bool active = true;
            var loaded = new UniTaskCompletionSource<bool>();
            var completed = new UniTaskCompletionSource<bool>();
            try
            {
                ad = SDK.CreateRewardedVideoAd(id,
                    () => { if (active) loaded.TrySetResult(true); },
                    (code, error) => { if (active) { loaded.TrySetResult(false); completed.TrySetResult(false); } },
                    reward => { if (active) completed.TrySetResult(reward); });
                ad.Load(() => { if (active) loaded.TrySetResult(true); }, error => { if (active) loaded.TrySetResult(false); });
                var loadResult = await UniTask.WhenAny(loaded.Task, UniTask.Delay(TimeSpan.FromSeconds(10), DelayType.Realtime).ContinueWith(() => false));
                if (loadResult.winArgumentIndex != 0 || !loadResult.result1) return false;
                ad.Show(() => { }, error => { if (active) completed.TrySetResult(false); });
                var showResult = await UniTask.WhenAny(completed.Task, UniTask.Delay(TimeSpan.FromSeconds(180), DelayType.Realtime).ContinueWith(() => false));
                return showResult.winArgumentIndex == 0 && showResult.result1;
            }
            catch { return false; }
            finally { active = false; ad?.Dispose(); }
        }
    }
}
