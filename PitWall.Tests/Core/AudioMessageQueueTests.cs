using System.Linq;
using PitWall.Core;
using PitWall.Models;
using Xunit;

namespace PitWall.Tests.Core
{
    public class AudioMessageQueueTests
    {
        [Fact]
        public void Enqueue_AddsMessageInOrder()
        {
            var queue = new AudioMessageQueue();
            queue.Enqueue(new Recommendation { Message = "First", Priority = Priority.Info });
            queue.Enqueue(new Recommendation { Message = "Second", Priority = Priority.Warning });

            var next = queue.Dequeue()!;
            Assert.Equal("First", next.Message);
            next = queue.Dequeue()!;
            Assert.Equal("Second", next.Message);
        }

        [Fact]
        public void Dequeue_WhenEmpty_ReturnsNull()
        {
            var queue = new AudioMessageQueue();
            Assert.Null(queue.Dequeue());
        }

        [Fact]
        public void Enqueue_DeduplicatesCriticalSameMessage()
        {
            var queue = new AudioMessageQueue();
            queue.Enqueue(new Recommendation { Message = "Box now", Priority = Priority.Critical, Type = RecommendationType.Fuel });
            queue.Enqueue(new Recommendation { Message = "Box now", Priority = Priority.Critical, Type = RecommendationType.Fuel });

            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void Enqueue_AllowsDifferentCriticalMessages()
        {
            var queue = new AudioMessageQueue();
            queue.Enqueue(new Recommendation { Message = "Box now", Priority = Priority.Critical, Type = RecommendationType.Fuel });
            queue.Enqueue(new Recommendation { Message = "Damage critical", Priority = Priority.Critical, Type = RecommendationType.Damage });

            Assert.Equal(2, queue.Count);
        }

        [Fact]
        public void Peek_DoesNotRemoveItem()
        {
            var queue = new AudioMessageQueue();
            queue.Enqueue(new Recommendation { Message = "First", Priority = Priority.Info });

            var peek = queue.Peek()!;
            Assert.Equal("First", peek.Message);
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void Clear_RemovesAllItems()
        {
            var queue = new AudioMessageQueue();
            queue.Enqueue(new Recommendation { Message = "A", Priority = Priority.Info });
            queue.Enqueue(new Recommendation { Message = "B", Priority = Priority.Info });

            queue.Clear();
            Assert.Equal(0, queue.Count);
            Assert.Null(queue.Dequeue());
        }
    }
}
