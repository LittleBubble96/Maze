using System;
using System.Collections.Generic;

namespace GameLogic.Maze
{
    [Serializable]
    public sealed class MazeLevelProgress
    {
        public MazeLevelData map;
        public int cell;
        public bool folded, freeHintUsed, completed, skipped;
        public bool pendingHint;
    }
    [Serializable]
    public sealed class MazeProgressData
    {
        public int schema = 1, currentLevel = 1, unlocked = 1;
        public List<MazeLevelProgress> levels = new List<MazeLevelProgress>();
        public string pendingRequest, pendingPurpose;
        public int pendingLevel;
    }
    public interface IMazeProgressStore
    {
        MazeProgressData Load();
        void Save(MazeProgressData data);
    }
}
