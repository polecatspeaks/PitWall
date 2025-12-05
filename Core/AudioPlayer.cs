using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Minimal audio player stub; in Phase 2 it dequeues messages and would trigger playback.
    /// </summary>
    public class AudioPlayer
    {
        private readonly AudioMessageQueue _queue;

        public AudioPlayer(AudioMessageQueue queue)
        {
            _queue = queue;
        }

        /// <summary>
        /// Dequeues next message and (in future) plays it. Returns true if something was dequeued.
        /// </summary>
        public bool PlayNext()
        {
            var next = _queue.Dequeue();
            if (next == null)
            {
                return false;
            }

            // TODO: Hook into SimHub audio output in later phase
            _ = next.Message;
            return true;
        }
    }
}
