using System.Collections.Generic;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Simple in-memory queue for audio recommendations with deduplication for critical messages.
    /// </summary>
    public class AudioMessageQueue
    {
        private readonly Queue<Recommendation> _queue = new();
        private readonly HashSet<string> _criticalDedup = new();

        public int Count => _queue.Count;

        public void Enqueue(Recommendation recommendation)
        {
            if (recommendation == null)
            {
                return;
            }

            if (recommendation.Priority == Priority.Critical)
            {
                string key = DedupKey(recommendation);
                if (_criticalDedup.Contains(key))
                {
                    return;
                }

                _criticalDedup.Add(key);
            }

            _queue.Enqueue(recommendation);
        }

        public Recommendation? Dequeue()
        {
            if (_queue.Count == 0) return null;
            var next = _queue.Dequeue();
            if (next.Priority == Priority.Critical)
            {
                _criticalDedup.Remove(DedupKey(next));
            }
            return next;
        }

        public Recommendation? Peek()
        {
            if (_queue.Count == 0) return null;
            return _queue.Peek();
        }

        public void Clear()
        {
            _queue.Clear();
            _criticalDedup.Clear();
        }

        private static string DedupKey(Recommendation rec)
        {
            return $"{rec.Type}:{rec.Priority}:{rec.Message}";
        }
    }
}
