using System;

namespace DontLookBack.ObservationMaze
{
    [Serializable]
    public sealed class DoorLinkCycle
    {
        private readonly int[] destinations;
        private int index;

        public DoorLinkCycle(params int[] destinations)
        {
            if (destinations == null || destinations.Length == 0)
            {
                throw new ArgumentException("A door needs at least one destination.", nameof(destinations));
            }

            this.destinations = (int[])destinations.Clone();
        }

        public int Current => destinations[index];
        public int ChangeCount { get; private set; }

        public bool TryShift(bool observed)
        {
            if (observed || destinations.Length < 2)
            {
                return false;
            }

            index = (index + 1) % destinations.Length;
            ChangeCount++;
            return true;
        }
    }
}
