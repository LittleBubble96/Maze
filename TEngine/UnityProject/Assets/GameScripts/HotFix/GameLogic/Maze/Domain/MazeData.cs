using System;

namespace GameLogic.Maze
{
    public enum MazeMode { Fold, Sokoban, DualBall }
    public enum MazeDirection { Up, Right, Down, Left }

    [Serializable]
    public sealed class MazeLevelData
    {
        public int id = 1, version = 1, seed, generatorVersion = 1;
        public int rows = 7, columns = 9, foldColumn = 4, foldWidth = 1;
        public int start, exit;
        public bool initialFolded, tutorial;
        public MazeMode mode;
        public int[] cells;
        public MazeLevelData Copy()
        {
            var copy = (MazeLevelData)MemberwiseClone();
            copy.cells = cells == null ? null : (int[])cells.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class MazeCampaignData { public MazeLevelData[] levels; }

    /// <summary>Runtime map owns its array. Rendering receives queries, never writable cells.</summary>
    public sealed class MazeMap
    {
        private readonly MazeLevelData _data;
        public int Id => _data.id;
        public int Version => _data.version;
        public int Rows => _data.rows;
        public int Columns => _data.columns;
        public int Count => Rows * Columns;
        public int FoldColumn => _data.foldColumn;
        public int FoldWidth => _data.foldWidth;
        public int Start => _data.start;
        public int Exit => _data.exit;
        public bool InitialFolded => _data.initialFolded;
        public bool Tutorial => _data.tutorial;
        public MazeMode Mode => _data.mode;
        public MazeMap(MazeLevelData data)
        {
            if (data == null || data.rows < 3 || data.columns < 3 || data.rows > 31 || data.columns > 31 ||
                data.cells == null || data.cells.Length != data.rows * data.columns ||
                data.foldColumn < 1 || data.foldWidth < 1 || data.foldColumn + data.foldWidth >= data.columns)
                throw new ArgumentException("地图尺寸或折叠区域无效");
            _data = data.Copy();
            foreach (int cell in _data.cells) if (cell != 0 && cell != 1) throw new ArgumentException("未知格子类型");
            if (data.id < 1 || data.version < 1 || !IsRoad(Start) || !IsRoad(Exit) || Start == Exit || IsMiddle(Start) || IsMiddle(Exit))
                throw new ArgumentException("起点、出口或地图标识无效");
        }
        public bool IsRoad(int cell) => cell >= 0 && cell < Count && _data.cells[cell] == 1;
        public bool IsMiddle(int cell) => cell % Columns >= FoldColumn && cell % Columns < FoldColumn + FoldWidth;
        public int Section(int cell) => cell % Columns < FoldColumn ? 0 : IsMiddle(cell) ? 1 : 2;
        public MazeLevelData Export() => _data.Copy();
    }

    public readonly struct MazeNode : IEquatable<MazeNode>
    {
        public readonly int Cell;
        public readonly bool Folded;
        public MazeNode(int cell, bool folded) { Cell = cell; Folded = folded; }
        public bool Equals(MazeNode other) => Cell == other.Cell && Folded == other.Folded;
        public override bool Equals(object obj) => obj is MazeNode other && Equals(other);
        public override int GetHashCode() => Cell * 2 + (Folded ? 1 : 0);
    }

    /// <summary>Mode-independent lifecycle. Sokoban and dual-ball get their own state and rule classes.</summary>
    public interface IMazeGameplay
    {
        MazeMode Mode { get; }
        MazeMap Map { get; }
        bool Completed { get; }
        event Action Checkpoint;
        void Restart();
        void CancelTransientAction();
    }
}
