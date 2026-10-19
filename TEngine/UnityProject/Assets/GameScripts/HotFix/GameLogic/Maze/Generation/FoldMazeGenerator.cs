using System;

namespace GameLogic.Maze
{
    /// <summary>Offline seeded candidate generation. A rejected map is never silently published.</summary>
    public static class FoldMazeGenerator
    {
        public const int Version = 1;
        public static MazeLevelData FirstTutorial()
        {
            var data = new MazeLevelData { id = 1, version = 2, rows = 5, columns = 7,
                foldColumn = 3, foldWidth = 1, start = 22, exit = 12, tutorial = true, cells = new int[35] };
            foreach (int cell in new[] { 22, 23, 24, 17, 10, 11, 12 }) data.cells[cell] = 1;
            return data;
        }
        private struct Random32
        {
            private uint _state;
            public Random32(int seed) { _state = (uint)seed; if (_state == 0) _state = 0x9e3779b9; }
            public uint Next() { _state ^= _state << 13; _state ^= _state >> 17; _state ^= _state << 5; return _state; }
            public int Range(int max) => (int)(Next() % (uint)max);
        }
        public static MazeLevelData Generate(int id, int seed, int minimumSwitches, int rows = 7, int columns = 9, int maxAttempts = 20000)
        {
            if (minimumSwitches < 0 || minimumSwitches > 5 || rows < 5 || columns < 7 || rows > 31 || columns > 31)
                throw new ArgumentOutOfRangeException("生成参数超出范围");
            var random = new Random32(seed);
            var data = new MazeLevelData
            {
                id = id, seed = seed, generatorVersion = Version, rows = rows, columns = columns,
                foldColumn = columns / 2, foldWidth = 1, tutorial = id <= 4,
                initialFolded = id == 8, cells = new int[rows * columns]
            };
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                for (int i = 0; i < data.cells.Length; i++) data.cells[i] = random.Range(100) < 48 ? 1 : 0;
                data.start = random.Range(rows) * columns + random.Range(data.foldColumn);
                data.exit = random.Range(rows) * columns + data.foldColumn + 1 + random.Range(columns - data.foldColumn - 1);
                data.cells[data.start] = data.cells[data.exit] = 1;
                var map = new MazeMap(data);
                var rules = new FoldMazeRules(map);
                var start = new MazeNode(map.Start, map.InitialFolded);
                if (rules.MinimumSwitches(start) != minimumSwitches) continue;
                var path = rules.Solve(start);
                if (path == null || path.Count < 5) continue;
                // Remove unreachable decoration, preserving both-state reachable roads.
                var reachable = Reachable(rules, start);
                for (int i = 0; i < data.cells.Length; i++) if (!reachable[i * 2] && !reachable[i * 2 + 1]) data.cells[i] = 0;
                return data.Copy();
            }
            throw new InvalidOperationException($"种子 {seed} 在 {maxAttempts} 次内未生成满足 {minimumSwitches} 次切换的关卡，请换种子");
        }
        public static bool[] Reachable(FoldMazeRules rules, MazeNode start)
        {
            var seen = new bool[rules.Map.Count * 2];
            var queue = new MazeNode[seen.Length];
            int head = 0, tail = 0;
            seen[start.GetHashCode()] = true;
            queue[tail++] = start;
            while (head < tail)
            {
                var node = queue[head++];
                for (int i = 0; i < 5; i++)
                {
                    MazeNode next;
                    if (!(i == 4 ? rules.TryToggle(node, out next) : rules.TryMove(node, (MazeDirection)i, out next))) continue;
                    if (seen[next.GetHashCode()]) continue;
                    seen[next.GetHashCode()] = true;
                    queue[tail++] = next;
                }
            }
            return seen;
        }
        public static string Validate(MazeLevelData data)
        {
            try
            {
                var map = new MazeMap(data);
                var rules = new FoldMazeRules(map);
                var start = new MazeNode(map.Start, map.InitialFolded);
                if (rules.Solve(start) == null) return "起点无法到达出口";
                // All edges are reversible: every reachable state can return to start and take its solution.
                var seen = Reachable(rules, start);
                for (int key = 0; key < seen.Length; key++)
                {
                    if (!seen[key]) continue;
                    var node = FoldMazeRules.Decode(key);
                    for (int d = 0; d < 4; d++)
                        if (rules.TryMove(node, (MazeDirection)d, out var next) &&
                            (!rules.TryMove(next, (MazeDirection)((d + 2) % 4), out var back) || !back.Equals(node)))
                            return "发现不可逆道路";
                }
                return null;
            }
            catch (Exception e) { return e.Message; }
        }
    }
}
