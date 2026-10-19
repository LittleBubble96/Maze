using System;
namespace GameLogic.Maze
{
    /// <summary>Validates hand-authored levels using the runtime topology.</summary>
    public static class MazeLevelValidator
    {
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
