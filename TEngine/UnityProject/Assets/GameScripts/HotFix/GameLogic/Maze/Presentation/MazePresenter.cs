using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic.Maze
{
    /// <summary>Composition root. Owns subscriptions, resources and application lifetime.</summary>
    public sealed class MazePresenter : MonoBehaviour
    {
        private MazeSession _session;
        private MazeHudView _screen;
        private MazeSceneView _scene;
        private MazeSceneInput _input;
        private IMazeRewardedVideo _ads;
        private bool _adBusy, _settled, _disposed;
        private bool _pendingFold;
        private bool _pendingSnap;
        private float _finishTime;

        public static async UniTask LaunchAsync()
        {
            TextAsset catalogAsset = null, adAsset = null;
            GameObject sceneRoot = null;
            try
            {
                catalogAsset = await GameModule.Resource.LoadAssetAsync<TextAsset>("MazeCampaign");
                adAsset = await GameModule.Resource.LoadAssetAsync<TextAsset>("MazeAds");
                sceneRoot = await GameModule.Resource.LoadGameObjectAsync("MazeSceneRoot");
                var window = await GameModule.UI.ShowUIAsyncAwait<UICorePlay>();
                if (catalogAsset == null || sceneRoot == null || window?.Hud == null)
                    throw new InvalidOperationException("迷宫场景或 UICorePlay 资源加载失败，请执行资源构建");
                Create(JsonUtility.FromJson<MazeCampaignData>(catalogAsset.text), sceneRoot.GetComponent<MazeSceneView>(), window.Hud,
                    new MazeProgressStore(), new MazeRewardedVideo(adAsset == null ? null : JsonUtility.FromJson<MazeAdConfig>(adAsset.text)));
                sceneRoot = null;
            }
            finally
            {
                if (catalogAsset != null) GameModule.Resource.UnloadAsset(catalogAsset);
                if (adAsset != null) GameModule.Resource.UnloadAsset(adAsset);
                if (sceneRoot != null) Destroy(sceneRoot);
            }
        }
        public static MazePresenter Create(MazeCampaignData campaign, MazeSceneView view, MazeHudView hud, IMazeProgressStore store, IMazeRewardedVideo ads)
        {
            if (view == null || hud == null) throw new ArgumentException("场景视图和 HUD 必须来自各自预制体");
            var presenter = view.gameObject.AddComponent<MazePresenter>();
            DontDestroyOnLoad(view.gameObject);
            presenter._session = new MazeSession(campaign, store);
            presenter._scene = view; presenter._screen = hud; presenter._ads = ads;
            hud.Initialize();
            presenter._input = view.gameObject.AddComponent<MazeSceneInput>();
            presenter._input.View = view;
            presenter.Connect();
            presenter.Open(Mathf.Clamp(presenter._session.Progress.currentLevel, 1, presenter._session.Progress.unlocked));
            return presenter;
        }
        private bool CanAct => !_disposed && !_adBusy && !_pendingFold && !_screen.Busy && !_scene.Busy && _session.Game != null && !_session.Game.Completed;
        private void Connect()
        {
            _input.CanInput = () => CanAct;
            _input.Begin = direction => { if (CanAct) _session.Game.BeginDrag(direction); };
            _input.Move = (direction, distance) => { if (!_adBusy) _session.Game.Drag(direction, distance); };
            _input.End = () =>
            {
                if (_session.Game.Completed) return;
                Vector3 before = _scene.BallPosition;
                int cell = _session.Game.Fraction > .5f ? _session.Game.Target.Cell : _session.Game.Anchor.Cell;
                _pendingSnap = true;
                _scene.Snap(before, cell);
            };
            _screen.Fold = () =>
            {
                if (!CanAct || _session.Game.Dragging) return;
                if (!_session.Game.Rules.TryToggle(_session.Game.Stable, out var next)) { _screen.SetMessage("请先走出折叠区域"); return; }
                _pendingFold = true;
                _scene.AnimateFold(next.Folded);
                _screen.SetMessage(next.Folded ? "折叠后，同一行道路相接即可通行" : "展开后，中间区域可以进入");
            };
            _screen.Restart = () =>
            {
                if (!CanAct || _session.Game.Dragging) return;
                _session.Game.Restart(); _scene.Bind(_session.Game.Map, _session.Game.Stable.Folded); ShowTeaching();
            };
            _screen.Hint = () =>
            {
                if (!CanAct || _session.Game.Dragging) return;
                if (_session.Game.RequestHint()) { _screen.SetMessage("沿箭头移动，到切换位置后点击高亮按钮"); return; }
                _screen.ShowDialog("看视频获得本次提示", "观看视频", () => Reward("hint").Forget());
            };
            _screen.Skip = () =>
            {
                if (!CanAct || !_session.CanSkip || _session.Game.Dragging) return;
                _screen.ShowDialog("看视频跳过本关\n本关标记为已跳过，可返回挑战", "看视频跳过", () => Reward("skip").Forget());
            };
            _screen.Select = ShowSelection;
            _session.SaveFailed += _screen.SetMessage;
        }
        private async UniTaskVoid Reward(string purpose)
        {
            string request = _session.BeginReward(purpose);
            if (request == null) return;
            _adBusy = true;
            _screen.SetMessage("视频加载中…");
            bool success = false;
            try { if (_ads != null) success = await _ads.ShowAsync(purpose); }
            catch (Exception error) { Debug.LogException(error); }
            if (_disposed) return;
            _session.FinishReward(request, success);
            _adBusy = false;
            if (success && purpose == "skip") { Bind(); return; }
            _screen.SetMessage(success ? "沿箭头继续前进" : "视频未完成或暂时无法播放，进度已保留");
        }
        private void Open(int id) { _session.Open(id); Bind(); }
        private void Bind()
        {
            _settled = false; _finishTime = 0;
            _pendingFold = false;
            _pendingSnap = false;
            _scene.Bind(_session.Game.Map, _session.Game.Stable.Folded); ShowTeaching();
        }
        private void ShowTeaching()
        {
            int id = _session.Game.Map.Id;
            _screen.SetMessage(id == 1 ? "向右滑动，再向上，最后向右到紫色目标\n这一关不需要折叠" : id == 2 ? "点击折叠，让断开的道路接起来" :
                id == 3 ? "跨过接缝后，再展开寻找新路线" : id == 4 ? "在不同位置尝试折叠和展开" : "经过紫色出口即可通关");
        }
        private void ShowSelection()
        {
            if (_adBusy || _session.Game.Dragging) return;
            _session.Suspend();
            _screen.ShowLevels(_session.LevelCount, _session.Progress.unlocked, id =>
            {
                if (id > _session.Progress.unlocked) return "未解锁";
                var p = _session.GetProgress(id);
                return p?.completed == true ? "已通关" : p?.skipped == true ? "已跳过" : "未通关";
            }, Open);
        }
        private void Update()
        {
            if (_session?.Game == null) return;
            _scene.Render(_session.Game);
            _screen.Render(_session.Game, _session.CanSkip, _adBusy || _scene.Busy);
            if (_pendingSnap && !_scene.Busy) { _pendingSnap = false; _session.Game.EndDrag(); }
            if (_pendingFold && !_scene.Busy) { _pendingFold = false; _session.Game.Toggle(); }
            if (!_session.Game.Completed || _settled) return;
            _finishTime += Time.unscaledDeltaTime;
            if (_finishTime < .4f) return;
            _settled = true;
            bool last = _session.Game.Map.Id == _session.LevelCount;
            _screen.ShowDialog(last ? "当前关卡已完成" : "通关成功", last ? "返回选关" : "下一关", () =>
            {
                if (last) ShowSelection(); else if (_session.Next()) Bind();
            }, false);
        }
        private void OnApplicationPause(bool paused)
        {
            if (!paused || _session?.Game == null) return;
            _pendingFold = false;
            _pendingSnap = false;
            _input.ResetPointer(); _session.Suspend(); _scene.ResetAnimation(_session.Game.Stable.Folded);
        }
        private void OnApplicationQuit() { _session?.Suspend(); }
        private void OnDestroy()
        {
            _disposed = true;
            if (_session != null) { _session.SaveFailed -= _screen.SetMessage; _session.Dispose(); }
            if (_screen != null) _screen.Fold = _screen.Hint = _screen.Restart = _screen.Skip = _screen.Select = null;
        }
    }
}

