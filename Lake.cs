using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Safari
{
    public class Lake
    {
        public int Capacity;
        public string Name { get; }
        // Array representing the animals currently in the lake
        private Animal?[] currentAnimals;
        // Mutex to protect critical sections in the lake
        private readonly Mutex lakeMutex = new Mutex();
        // Semaphore to represent available capacity in the lake (flamingo units)
        private readonly Semaphore lakeSemaphore;
        // True if a hippo currently occupies the lake
        private bool hippoInside = false;
        // Queue of flamingos waiting for proper placement
        private readonly Queue<Animal> waitingFlamingos = new Queue<Animal>();
        // used to wake one waiting flamingo when space becomes available
        private readonly SemaphoreSlim flamingoWaiter = new SemaphoreSlim(0);
        // UI or external refresh trigger
        public Action? OnStatusChanged;

        public Lake(string name, int capacity)
        {
            Name = name;
            Capacity = capacity;
            currentAnimals = new Animal?[capacity];
            // Semaphore initialized to max capacity
            lakeSemaphore = new Semaphore(capacity, capacity);
        }

        // Returns a copy of the current animals in the lake (thread-safe)
        public Animal?[] GetCurrentAnimals()
        {
            lakeMutex.WaitOne();
            try
            {
                return currentAnimals.ToArray();
            }
            finally
            {
                lakeMutex.ReleaseMutex();
            }
        }

        // Main method that tries to place an animal into the lake
        public bool TryEnterLake(Animal animal)
        {
            // If hippo in the lake, the animal waits 
            while (true)
            {
                lakeMutex.WaitOne();
                try
                {
                    if (!hippoInside || animal.Type == AnimalType.Hippopotamus)
                        break;
                }
                finally
                {
                    lakeMutex.ReleaseMutex();
                }
                Thread.Sleep(100);
            }
            lakeMutex.WaitOne();
            try
            {
                // Hippo logic - remove all animals and takes all spots
                if (animal.Type == AnimalType.Hippopotamus)
                {
                    if (hippoInside) return false;

                    // Remove all current animals
                    var animalsToKick = currentAnimals.Where(a => a != null).Distinct().ToList();
                    for (int i = 0; i < Capacity; i++)
                        currentAnimals[i] = null;
                    int currentAvailable = 0;
                    while (lakeSemaphore.WaitOne(0))
                        currentAvailable++;
                    if (currentAvailable > 0)
                        lakeSemaphore.Release(currentAvailable);
                    int permitsToRelease = Capacity - currentAvailable;
                    if (permitsToRelease > 0)
                        lakeSemaphore.Release(permitsToRelease);
                    for (int i = 0; i < Capacity; i++)
                    {
                        lakeSemaphore.WaitOne();
                    }

                    // Fill all spots with hippo
                    for (int i = 0; i < Capacity; i++)
                        currentAnimals[i] = animal;

                    hippoInside = true;
                    OnStatusChanged?.Invoke();

                    return true;
                }

                // Check available space
                int requiredSpace = GetAnimalSize(animal);
                if (GetAvailableUnits() < requiredSpace)
                    return false;

                // Flamingo logic - must be adjacent to another flamingo
                if (animal.Type == AnimalType.Flamingo)
                {
                    bool foundFlamingo = false;

                    // Try to place next to existing flamingo
                    for (int i = 0; i < Capacity; i++)
                    {
                        if (currentAnimals[i]?.Type == AnimalType.Flamingo)
                        {
                            foundFlamingo = true;

                            int right = (i + 1) % Capacity;
                            int left = (i - 1 + Capacity) % Capacity;

                            if (currentAnimals[right] == null)
                            {
                                lakeSemaphore.WaitOne();
                                currentAnimals[right] = animal;
                                OnStatusChanged?.Invoke();
                                return true;
                            }
                            if (currentAnimals[left] == null)
                            {
                                lakeSemaphore.WaitOne();
                                currentAnimals[left] = animal;
                                OnStatusChanged?.Invoke();
                                return true;
                            }
                        }
                    }

                    // If no flamingo found, place in first available spot
                    if (!foundFlamingo)
                    {
                        for (int i = 0; i < Capacity; i++)
                        {
                            if (currentAnimals[i] == null)
                            {
                                lakeSemaphore.WaitOne();
                                currentAnimals[i] = animal;
                                OnStatusChanged?.Invoke();
                                return true;
                            }
                        }
                        return false;
                    }

                    // Need to wait for space
                    waitingFlamingos.Enqueue(animal);
                }
                else
                {
                    // Zebra logic - takes 2 spots
                    int size = GetAnimalSize(animal);

                    // Try to acquire required semaphore permits
                    for (int i = 0; i < size; i++)
                    {
                        if (!lakeSemaphore.WaitOne(0))
                        {
                            // Release permits we already acquired
                            for (int j = 0; j < i; j++)
                                lakeSemaphore.Release();
                            return false;
                        }
                    }

                    // Find spots for the animal
                    int added = 0;
                    for (int i = 0; i < Capacity && added < size; i++)
                    {
                        if (currentAnimals[i] == null)
                        {
                            currentAnimals[i] = animal;
                            added++;
                        }
                    }

                    if (added == size)
                    {
                        OnStatusChanged?.Invoke();
                        return true;
                    }
                    else
                    {
                        // Something went wrong, release semaphore permits
                        lakeSemaphore.Release(size);
                        return false;
                    }
                }
            }
            finally
            {
                lakeMutex.ReleaseMutex();
            }

            // Wait for flamingo placement
            if (animal.Type == AnimalType.Flamingo)
            {
                flamingoWaiter.Wait();
                lakeMutex.WaitOne();
                try
                {
                    return currentAnimals.Contains(animal);
                }
                finally
                {
                    lakeMutex.ReleaseMutex();
                }
            }

            return false;
        }

        // Called when an animal leaves the lake
        public void LeaveLake(Animal animal)
        {
            lakeMutex.WaitOne();
            try
            {
                int releasedSpots = 0;

                // Remove the animal from all its occupied slots
                for (int i = 0; i < Capacity; i++)
                {
                    if (currentAnimals[i] == animal)
                    {
                        currentAnimals[i] = null;
                        releasedSpots++;
                    }
                }

                // Handle Hippo case
                if (animal.Type == AnimalType.Hippopotamus)
                {
                    hippoInside = false;
                    lakeSemaphore.Release(Capacity);
                }
                else if (releasedSpots > 0)
                {
                    lakeSemaphore.Release(releasedSpots);
                }

                // Try to place waiting flamingos
                TryWakeWaitingFlamingos();

                OnStatusChanged?.Invoke();
            }
            finally
            {
                lakeMutex.ReleaseMutex();
            }
        }

        // Returns the number of empty slots
        private int GetAvailableUnits()
        {
            return currentAnimals.Count(a => a == null);
        }

        // Returns size required in slots for the given animal
        private int GetAnimalSize(Animal animal)
        {
            return animal.Type == AnimalType.Zebra ? 2 : 1;
        }

        // Attempts to wake waiting flamingos if space next to existing ones is available
        private void TryWakeWaitingFlamingos()
        {
            if (waitingFlamingos.Count == 0)
                return;

            var flamingosToRequeue = new Queue<Animal>();

            while (waitingFlamingos.Count > 0)
            {
                var flamingo = waitingFlamingos.Dequeue();
                bool placed = false;

                // Try to place next to existing flamingo
                for (int i = 0; i < Capacity; i++)
                {
                    if (currentAnimals[i]?.Type == AnimalType.Flamingo)
                    {
                        int right = (i + 1) % Capacity;
                        int left = (i - 1 + Capacity) % Capacity;

                        if (currentAnimals[right] == null)
                        {
                            lakeSemaphore.WaitOne();
                            currentAnimals[right] = flamingo;
                            placed = true;
                            break;
                        }
                        else if (currentAnimals[left] == null)
                        {
                            lakeSemaphore.WaitOne();
                            currentAnimals[left] = flamingo;
                            placed = true;
                            break;
                        }
                    }
                }

                if (placed)
                {
                    flamingoWaiter.Release();
                    OnStatusChanged?.Invoke();
                }
                else
                {
                    flamingosToRequeue.Enqueue(flamingo);
                }
            }

            // Re-queue flamingos that couldn't be placed
            foreach (var f in flamingosToRequeue)
                waitingFlamingos.Enqueue(f);
        }
    }
}