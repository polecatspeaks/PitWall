using PitWall.Core;
using PitWall.Models;
using Xunit;

namespace PitWall.Tests.Core
{
    public class AudioPlayerTests
    {
        [Fact]
        public void PlayNext_WhenQueueEmpty_DoesNothing()
        {
            var queue = new AudioMessageQueue();
            var player = new AudioPlayer(queue);

            var played = player.PlayNext();

            Assert.False(played);
        }

        [Fact]
        public void PlayNext_PlaysAndDequeuesMessage()
        {
            var queue = new AudioMessageQueue();
            queue.Enqueue(new Recommendation { Message = "Test", Priority = Priority.Info });
            var player = new AudioPlayer(queue);

            var played = player.PlayNext();

            Assert.True(played);
            Assert.Equal(0, queue.Count);
        }
    }
}
