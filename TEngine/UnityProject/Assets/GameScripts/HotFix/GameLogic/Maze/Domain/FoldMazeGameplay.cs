using System;
using System.Collections.Generic;

namespace GameLogic.Maze
{
    /// <summary>Continuous movement on a discrete topology. Only stable checkpoints are persisted.</summary>
    public sealed class FoldMazeGameplay : IMazeGameplay
    {
        public MazeMode Mode => MazeMode.Fold;
        public MazeMap Map => Rules.Map;
        public FoldMazeRules Rules { get; }
        public MazeNode Stable { get; private set; }
        public MazeNode Anchor { get; private set; }
        public MazeNode Target { get; private set; }
        public float Fraction { get; private set; }
        public bool Dragging { get; private set; }
        public bool Completed { get; private set; }
        public bool FreeHintUsed { get; private set; }
        public int HintRevision { get; private set; }
        public IReadOnlyList<MazeNode> Hint => _hint;
        private readonly List<MazeNode> _hint = new List<MazeNode>();
        private MazeDirection _direction;
        private MazeDirection _segmentDirection;
        public event Action Checkpoint;

        public FoldMazeGameplay(MazeMap map, MazeNode? restored = null, bool hintUsed = false, bool completed = false)
        {
            Rules = new FoldMazeRules(map);
            Stable = restored.HasValue && Rules.IsValid(restored.Value) ? restored.Value : new MazeNode(map.Start, map.InitialFolded);
            Anchor = Target = Stable;
            FreeHintUsed = hintUsed;
            Completed = completed;
        }
        public bool BeginDrag(MazeDirection direction)
        {
            if (Completed || Dragging) return false;
            Anchor = Target = Stable;
            Fraction = 0;
            _direction = direction;
            _segmentDirection = direction;
            Dragging = true;
            return true;
        }
        /// <summary>Signed distance in visible cell widths; consumes every crossed edge, including exit entry.</summary>
        public void Drag(float distance)
        {
            if (!Dragging || Completed || float.IsNaN(distance) || float.IsInfinity(distance)) return;
            var desired = distance < 0 ? (MazeDirection)(((int)_direction + 2) % 4) : _direction;
            distance = Math.Abs(distance);
            if (Fraction > 0 && desired != _segmentDirection)
            {
                float back = Math.Min(distance, Fraction);
                Fraction -= back;
                distance -= back;
                if (distance == 0) return;
            }
            _segmentDirection = desired;
            // Pointer jumps are bounded by the map extent; no unbounded per-frame work.
            distance = Math.Min(distance, Map.Count);
            while (distance > 0 && !Completed)
            {
                if (!Rules.TryMove(Anchor, desired, out var next)) { Target = Anchor; Fraction = 0; return; }
                Target = next;
                float used = Math.Min(distance, 1 - Fraction);
                Fraction += used;
                distance -= used;
                if (Target.Cell == Map.Exit && Fraction >= 0.5f) { Complete(Target); return; }
                if (Fraction < 1) break;
                Anchor = Target;
                Fraction = 0;
                VisitHint(Anchor);
            }
        }
        public void Drag(MazeDirection direction, float distance)
        {
            if (!Dragging || Completed || distance <= 0 || float.IsNaN(distance) || float.IsInfinity(distance)) return;
            if (Fraction > 0 && ((int)direction % 2 != (int)_segmentDirection % 2))
            {
                var corner = Fraction > .5f ? Target : Anchor;
                if (!Rules.TryMove(corner, direction, out _)) return;
                Anchor = Target = corner;
                Fraction = 0;
                VisitHint(corner);
            }
            _direction = direction;
            Drag(distance);
        }
        public void EndDrag()
        {
            if (!Dragging || Completed) return;
            // Exactly half-way returns to the last reached centre.
            Stable = Fraction > 0.5f ? Target : Anchor;
            VisitHint(Stable);
            Anchor = Target = Stable;
            Fraction = 0;
            Dragging = false;
            Checkpoint?.Invoke();
        }
        public void CancelTransientAction()
        {
            if (Dragging && _hint.Count > 0) { _hint.Clear(); HintRevision++; }
            Dragging = false;
            Anchor = Target = Stable;
            Fraction = 0;
        }
        public bool Toggle()
        {
            if (Dragging || Completed || !Rules.TryToggle(Stable, out var next)) return false;
            Stable = Anchor = Target = next;
            VisitHint(next);
            Checkpoint?.Invoke();
            return true;
        }
        public void Restart()
        {
            Completed = false;
            Stable = new MazeNode(Map.Start, Map.InitialFolded);
            CancelTransientAction();
            _hint.Clear();
            HintRevision++;
            Checkpoint?.Invoke();
        }
        public bool RequestHint(bool rewarded = false)
        {
            if (Dragging || Completed) return false;
            if (_hint.Count > 0) return true;
            if (FreeHintUsed && !rewarded) return false;
            var solution = Rules.Solve(Stable);
            if (solution == null || solution.Count == 0) return false;
            _lastHintNode = Stable;
            foreach (var node in solution)
            {
                _hint.Add(node);
                if (node.Folded != Stable.Folded || node.Cell == Map.Exit) break;
            }
            FreeHintUsed = true;
            HintRevision++;
            Checkpoint?.Invoke();
            return true;
        }
        private MazeNode _lastHintNode;
        private void VisitHint(MazeNode node)
        {
            if (_hint.Count == 0 || node.Equals(_lastHintNode)) return;
            if (_hint[0].Equals(node)) { _lastHintNode = node; _hint.RemoveAt(0); }
            else _hint.Clear();
            HintRevision++;
        }
        private void Complete(MazeNode exit)
        {
            Completed = true;
            Stable = exit;
            CancelTransientAction();
            _hint.Clear();
            HintRevision++;
            Checkpoint?.Invoke();
        }
    }
}
