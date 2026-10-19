using System;
using System.Collections.Generic;

namespace GameLogic.Maze
{
    /// <summary>Campaign orchestration only; continuous movement and fold rules live in FoldMazeGameplay.</summary>
    public sealed class MazeSession : IDisposable
    {
        private readonly IMazeProgressStore _store;
        private readonly Dictionary<int, MazeLevelData> _catalog = new Dictionary<int, MazeLevelData>();
        public MazeProgressData Progress { get; }
        public FoldMazeGameplay Game { get; private set; }
        public int LevelCount { get; }
        public event Action Changed;
        public event Action<string> SaveFailed;
        public MazeSession(MazeCampaignData campaign, IMazeProgressStore store)
        {
            if (campaign?.levels == null || campaign.levels.Length == 0) throw new ArgumentException("关卡目录为空");
            _store = store;
            LevelCount = campaign.levels.Length;
            for (int i = 0; i < LevelCount; i++)
            {
                var data = campaign.levels[i];
                if (data.id != i + 1) throw new ArgumentException("首版关卡 ID 必须从 1 连续排列");
                string error = FoldMazeGenerator.Validate(data);
                if (error != null) throw new ArgumentException($"关卡 {data.id}: {error}");
                _catalog.Add(data.id, data.Copy());
            }
            Progress = store.Load() ?? new MazeProgressData();
            Progress.unlocked = Math.Max(1, Math.Min(Progress.unlocked, LevelCount));
            // An unconfirmed interrupted ad is not a reward. Confirmed hint credits survive separately.
            Progress.pendingRequest = Progress.pendingPurpose = null;
            Progress.pendingLevel = 0;
        }
        public MazeLevelProgress GetProgress(int id) => Progress.levels.Find(p => p != null && p.map != null && p.map.id == id);
        public void Open(int id)
        {
            if (id < 1 || id > Progress.unlocked || !_catalog.TryGetValue(id, out var configured)) return;
            if (Game != null) { Game.CancelTransientAction(); Game.Checkpoint -= Commit; }
            var progress = GetProgress(id);
            if (progress == null)
            {
                progress = new MazeLevelProgress { map = configured.Copy(), cell = configured.start, folded = configured.initialFolded };
                Progress.levels.Add(progress);
            }
            // Keep the exact generated map, not just a seed whose algorithm might change later.
            if (progress.map.version != configured.version || FoldMazeGenerator.Validate(progress.map) != null)
            {
                progress.map = configured.Copy(); progress.cell = configured.start; progress.folded = configured.initialFolded;
            }
            var map = new MazeMap(progress.map);
            Game = new FoldMazeGameplay(map, progress.completed ? new MazeNode(map.Start, map.InitialFolded) :
                new MazeNode(progress.cell, progress.folded), progress.freeHintUsed);
            Game.Checkpoint += Commit;
            Progress.currentLevel = id;
            if (progress.pendingHint && Game.RequestHint(true)) progress.pendingHint = false;
            Commit();
        }
        public bool CanSkip => Game != null && !Game.Map.Tutorial && Game.Map.Id < LevelCount && !Game.Completed && !GetProgress(Game.Map.Id).completed;
        public bool Next()
        {
            if (Game == null || !Game.Completed || Game.Map.Id >= LevelCount) return false;
            Open(Game.Map.Id + 1); return true;
        }
        public void Commit()
        {
            if (Game == null) return;
            var p = GetProgress(Game.Map.Id);
            p.cell = Game.Stable.Cell; p.folded = Game.Stable.Folded;
            p.freeHintUsed = Game.FreeHintUsed;
            if (Game.Completed)
            {
                p.completed = true;
                Progress.unlocked = Math.Max(Progress.unlocked, Math.Min(LevelCount, Game.Map.Id + 1));
            }
            Save(); Changed?.Invoke();
        }
        public string BeginReward(string purpose)
        {
            if (Game == null || Game.Completed || Game.Dragging || Progress.pendingRequest != null ||
                (purpose != "hint" && purpose != "skip") || (purpose == "skip" && !CanSkip)) return null;
            if (purpose == "hint" && (Game.Hint.Count > 0 || Game.Rules.Solve(Game.Stable) == null)) return null;
            Progress.pendingRequest = Guid.NewGuid().ToString("N");
            Progress.pendingPurpose = purpose; Progress.pendingLevel = Game.Map.Id;
            Save(); return Progress.pendingRequest;
        }
        public bool FinishReward(string request, bool completed)
        {
            if (request == null || Progress.pendingRequest != request) return false;
            int id = Progress.pendingLevel;
            string purpose = Progress.pendingPurpose;
            Progress.pendingRequest = Progress.pendingPurpose = null;
            Progress.pendingLevel = 0;
            var p = GetProgress(id);
            if (!completed || p == null) { Save(); return false; }
            if (purpose == "hint") p.pendingHint = true;
            else if (purpose == "skip") { p.skipped = true; Progress.unlocked = Math.Max(Progress.unlocked, Math.Min(LevelCount, id + 1)); }
            Save(); // Confirmed reward + request consumption are one snapshot, before presentation.
            if (Game != null && Game.Map.Id == id)
            {
                if (purpose == "hint" && Game.RequestHint(true)) { p.pendingHint = false; Save(); }
                else if (purpose == "skip") Open(id + 1);
            }
            Changed?.Invoke(); return true;
        }
        public void Suspend() { Game?.CancelTransientAction(); Commit(); }
        private void Save()
        {
            try { _store.Save(Progress); }
            catch (Exception error) { SaveFailed?.Invoke("进度保存失败：" + error.Message); }
        }
        public void Dispose() { Suspend(); if (Game != null) Game.Checkpoint -= Commit; }
    }
}
