using System;
using System.Threading;

namespace OpenGSServer
{
    // Simple token-bucket rate limiter
    internal class TokenBucket
    {
        private readonly double _capacity;
        private readonly double _refillPerSecond;
        private double _tokens;
        private DateTime _lastRefill;
        private readonly object _lock = new();

        public TokenBucket(double capacity, double refillPerSecond)
        {
            _capacity = Math.Max(1.0, capacity);
            _refillPerSecond = Math.Max(0.0, refillPerSecond);
            _tokens = _capacity;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryConsume(int amount)
        {
            if (amount <= 0) return true;

            lock (_lock)
            {
                Refill();
                if (_tokens >= amount)
                {
                    _tokens -= amount;
                    return true;
                }

                return false;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefill).TotalSeconds;
            if (elapsed <= 0) return;
            var add = elapsed * _refillPerSecond;
            if (add > 0)
            {
                _tokens = Math.Min(_capacity, _tokens + add);
                _lastRefill = now;
            }
        }
    }
}
