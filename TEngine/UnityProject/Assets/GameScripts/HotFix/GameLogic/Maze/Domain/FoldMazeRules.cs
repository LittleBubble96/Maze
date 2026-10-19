using System;
using System.Collections.Generic;

namespace GameLogic.Maze
{
    /// <summary>No Unity, transforms, physics or IO. Shared by runtime, generator, hint and editor.</summary>
    public sealed class FoldMazeRules
    {
        public MazeMap Map { get; }
        public FoldMazeRules(MazeMap map)
        {
            if (map.Mode != MazeMode.Fold) throw new NotSupportedException("此规则仅处理折叠迷宫");
            Map = map;
        }
        public bool IsValid(MazeNode node) => Map.IsRoad(node.Cell) && (!node.Folded || !Map.IsMiddle(node.Cell));
        public bool TryMove(MazeNode from, MazeDirection direction, out MazeNode next)
        {
            next = from;
            if (!IsValid(from)) return false;
            int row = from.Cell / Map.Columns, col = from.Cell % Map.Columns;
            switch (direction)
            {
                case MazeDirection.Up: row--; break;
                case MazeDirection.Down: row++; break;
                case MazeDirection.Right:
                    col += from.Folded && col == Map.FoldColumn - 1 ? Map.FoldWidth + 1 : 1; break;
                case MazeDirection.Left:
                    col -= from.Folded && col == Map.FoldColumn + Map.FoldWidth ? Map.FoldWidth + 1 : 1; break;
                default: return false;
            }
            if (row < 0 || row >= Map.Rows || col < 0 || col >= Map.Columns) return false;
            var target = new MazeNode(row * Map.Columns + col, from.Folded);
            if (!IsValid(target)) return false;
            next = target;
            return true;
        }
        public bool TryToggle(MazeNode from, out MazeNode next)
        {
            next = from;
            if (!IsValid(from) || (!from.Folded && Map.IsMiddle(from.Cell))) return false;
            next = new MazeNode(from.Cell, !from.Folded);
            return true;
        }

        public List<MazeNode> Solve(MazeNode start)
        {
            if (!IsValid(start)) return null;
            int count = Map.Count * 2;
            var previous = new int[count];
            for (int i = 0; i < count; i++) previous[i] = -1;
            var queue = new int[count];
            int head = 0, tail = 0, first = start.GetHashCode();
            queue[tail++] = first;
            previous[first] = first;
            while (head < tail)
            {
                int key = queue[head++];
                var node = Decode(key);
                if (node.Cell == Map.Exit)
                {
                    var path = new List<MazeNode>();
                    for (int k = key; k != first; k = previous[k]) path.Add(Decode(k));
                    path.Reverse();
                    return path;
                }
                for (int action = 0; action < 5; action++)
                {
                    MazeNode next;
                    bool valid = action == 4 ? TryToggle(node, out next) : TryMove(node, (MazeDirection)action, out next);
                    if (!valid || previous[next.GetHashCode()] != -1) continue;
                    previous[next.GetHashCode()] = key;
                    queue[tail++] = next.GetHashCode();
                }
            }
            return null;
        }
        public static MazeNode Decode(int key) => new MazeNode(key / 2, (key & 1) == 1);

        /// <summary>Independent 0-1 shortest path: walking costs zero, switching costs one.</summary>
        public int MinimumSwitches(MazeNode start)
        {
            if (!IsValid(start)) return -1;
            var distance = new int[Map.Count * 2];
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;
            var deque = new LinkedList<MazeNode>();
            distance[start.GetHashCode()] = 0;
            deque.AddFirst(start);
            while (deque.Count > 0)
            {
                MazeNode node = deque.First.Value;
                deque.RemoveFirst();
                for (int action = 0; action < 5; action++)
                {
                    MazeNode next;
                    bool valid = action == 4 ? TryToggle(node, out next) : TryMove(node, (MazeDirection)action, out next);
                    if (!valid) continue;
                    int cost = distance[node.GetHashCode()] + (action == 4 ? 1 : 0);
                    if (cost >= distance[next.GetHashCode()]) continue;
                    distance[next.GetHashCode()] = cost;
                    if (action == 4) deque.AddLast(next); else deque.AddFirst(next);
                }
            }
            int result = Math.Min(distance[Map.Exit * 2], distance[Map.Exit * 2 + 1]);
            return result == int.MaxValue ? -1 : result;
        }
    }
}
