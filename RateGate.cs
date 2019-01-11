using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CVV
{
    /// <summary>
    /// Used to control the rate of some occurrence per unit of time.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     To control the rate of an action using a <see cref="RateGate"/>, 
    ///     code should simply call <see cref="WaitToProceed()"/> prior to 
    ///     performing the action. <see cref="WaitToProceed()"/> will block
    ///     the current thread until the action is allowed based on the rate 
    ///     limit.
    ///     </para>
    ///     <para>
    ///     This class is thread safe. A single <see cref="RateGate"/> instance 
    ///     may be used to control the rate of an occurrence across multiple 
    ///     threads.
    ///     </para>
    /// </remarks>
    public class RateGate : Disposable
    {
        // Semaphore used to count and limit the number of occurrences per
        // unit time.
        private readonly SemaphoreSlim m_Semaphore;

        // Times (in millisecond ticks) at which the semaphore should be exited.
        private readonly ConcurrentQueue<int> m_ExitTimes;

        // Timer used to trigger exiting the semaphore.
        private readonly Timer m_ExitTimer;

        /// <summary>
        /// Number of occurrences allowed per unit of time.
        /// </summary>
        public int Occurrences { get; private set; }

        /// <summary>
        /// The length of the time unit, in milliseconds.
        /// </summary>
        public int TimeUnitMilliseconds { get; private set; }

        /// <summary>
        /// Initializes a <see cref="RateGate"/> with a rate of <paramref name="occurrences"/> 
        /// per <paramref name="timeUnit"/>.
        /// </summary>
        /// <param name="occurrences">Number of occurrences allowed per unit of time.</param>
        /// <param name="timeUnit">Length of the time unit.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="occurrences"/> or <paramref name="timeUnit"/> is negative.
        /// </exception>
        public RateGate(int occurrences, TimeSpan timeUnit)
        {
            // Check the arguments.
            if (occurrences <= 0)
            {
                throw new ArgumentOutOfRangeException("occurrences", "Number of occurrences must be a positive integer");
            }
            if (timeUnit != timeUnit.Duration())
            {
                throw new ArgumentOutOfRangeException("timeUnit", "Time unit must be a positive span of time");
            }
            if (timeUnit > TimeSpan.FromMilliseconds(int.MaxValue))
            {
                throw new ArgumentOutOfRangeException("timeUnit", $"Time unit must be less than {int.MaxValue} milliseconds");
            }

            Occurrences = occurrences;
            TimeUnitMilliseconds = (int)timeUnit.TotalMilliseconds;

            // Create the semaphore, with the number of occurrences as the maximum count.
            m_Semaphore = new SemaphoreSlim(Occurrences, Occurrences);

            // Create a queue to hold the semaphore exit times.
            m_ExitTimes = new ConcurrentQueue<int>();

            // Create a timer to exit the semaphore. Use the time unit as the original
            // interval length because that's the earliest we will need to exit the semaphore.
            m_ExitTimer = new Timer(ExitTimerCallback, null, TimeUnitMilliseconds, Timeout.Infinite);
        }

        // Callback for the exit timer that exits the semaphore based on exit times 
        // in the queue and then sets the timer for the nextexit time.
        private void ExitTimerCallback(object state)
        {
            lock (NoDisposeWhileLocked)
            {
                // While there are exit times that are passed due still in the queue,
                // exit the semaphore and dequeue the exit time.
                int exitTime;
                while (m_ExitTimes.TryPeek(out exitTime) && (unchecked(exitTime - Environment.TickCount) <= 0))
                {
                    m_Semaphore.Release();
                    m_ExitTimes.TryDequeue(out exitTime);
                }

                // Try to get the next exit time from the queue and compute
                // the time until the next check should take place. If the 
                // queue is empty, then no exit times will occur until at least
                // one time unit has passed.
                int timeUntilNextCheck;
                if (m_ExitTimes.TryPeek(out exitTime))
                {
                    timeUntilNextCheck = unchecked(exitTime - Environment.TickCount);
                }
                else
                {
                    timeUntilNextCheck = TimeUnitMilliseconds;
                }

                // Set the timer.
                m_ExitTimer.Change(timeUntilNextCheck, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the
        /// specified timeout elapses.
        /// </summary>
        /// <param name="millisecondsTimeout">Number of milliseconds to wait, or Timeout.Infinite to wait indefinitely.</param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        public bool WaitToProceed(int millisecondsTimeout)
        {
            // Check the arguments.
            if (millisecondsTimeout < 0 && millisecondsTimeout != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException("millisecondsTimeout");
            }

            AssertSafe();

            // Block until we can enter the semaphore or until the timeout expires.
            bool entered = m_Semaphore.Wait(millisecondsTimeout);

            // If we entered the semaphore, compute the corresponding exit time 
            // and add it to the queue.
            if (entered)
            {
                int timeToExit = unchecked(Environment.TickCount + TimeUnitMilliseconds);
                m_ExitTimes.Enqueue(timeToExit);
            }

            return entered;
        }

        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the
        /// specified timeout elapses.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        public bool WaitToProceed(TimeSpan timeout)
        {
            return WaitToProceed((int)timeout.TotalMilliseconds);
        }

        /// <summary>
        /// Blocks the current thread indefinitely until allowed to proceed.
        /// </summary>
        public void WaitToProceed()
        {
            WaitToProceed(Timeout.Infinite);
        }

        /// <summary>
        /// Releases resources held by an instance of this class.
        /// </summary>
        protected override void CleanUpResources()
        {
            try
            {
                // The semaphore and timer both implement IDisposable and 
                // therefore must be disposed.
                m_Semaphore.Dispose();
                m_ExitTimer.Dispose();
            }
            finally
            {
                base.CleanUpResources();
            }
        }
    }
}
