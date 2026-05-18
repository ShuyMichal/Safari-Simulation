using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Safari
{
    public class Animal
    {
        // Type of animal (e.g., Flamingo, Zebra, Hippopotamus)
        public AnimalType Type { get; }
        // The lake this animal is assigned to
        public Lake AssignedLake { get; }
        // Average arrival time (in seconds)
        public double ArrivalTime { get; }
        // Average drinking time (in seconds)
        public double DrinkTime { get; }

        // Constructor: initializes the animal's type, assigned lake, and timing parameters
        public Animal(AnimalType type, Lake lake)
        {
            Type = type;
            AssignedLake = lake;
            ArrivalTime = GetMeanArrival(type);
            DrinkTime = GetMeanDrink(type);
        }
        // Main life cycle of the animal (with random arrival and drink time)
        public void Live()
        {
            // Simulate arrival delay using a normally distributed time
            Thread.Sleep((int)(GetRandomNormal(ArrivalTime) * 1000));
            Console.WriteLine($"{GetAnimal(Type)} arrived at Lake {AssignedLake.Name}");

            // Attempt to enter the lake; if full or blocked, retry after short sleep
            while (!AssignedLake.TryEnterLake(this))
            {
                Thread.Sleep(200);
            }

            Console.WriteLine($"{GetAnimal(Type)} entered Lake {AssignedLake.Name}");

            // Simulate drinking time
            Thread.Sleep((int)(GetRandomNormal(DrinkTime) * 1000));

            // Leave the lake
            AssignedLake.LeaveLake(this);
            Console.WriteLine($"{GetAnimal(Type)} left Lake {AssignedLake.Name}");
        }

        // Alternate lifecycle method that just stays for a fixed duration
        public void Live(int durationMs)
        {
            if (!AssignedLake.TryEnterLake(this)) return;

            Thread.Sleep(durationMs);
            AssignedLake.LeaveLake(this);
        }

        // Returns the mean arrival time based on animal type
        private static double GetMeanArrival(AnimalType type) => type switch
        {
            AnimalType.Flamingo => 2.0,
            AnimalType.Zebra => 3.0,
            AnimalType.Hippopotamus => 10.0,
            _ => 1.0
        };

        // Returns the mean drink time based on animal type
        private static double GetMeanDrink(AnimalType type) => type switch
        {
            AnimalType.Flamingo => 3.5,
            AnimalType.Zebra => 5.0,
            AnimalType.Hippopotamus => 5.0,
            _ => 1.0
        };

        // Random number generator for all instances
        private static readonly Random rand = new Random();
        // Mutex to synchronize access to the RNG (thread safety)
        private static readonly Mutex randMutex = new Mutex();

        // Generate a random value with a normal distribution centered around the mean
        private static double GetRandomNormal(double mean)
        {
            randMutex.WaitOne();
            try
            {
                double u1 = 1.0 - rand.NextDouble(); // uniform(0,1] random number
                double u2 = 1.0 - rand.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                return Math.Max(0.1, mean + randStdNormal); // avoid negative sleep time
            }
            finally
            {
                randMutex.ReleaseMutex();
            }
        }

        // Returns a one-character symbol for the animal
        private static string GetAnimal(AnimalType type) => type switch
        {
            AnimalType.Flamingo => "F",
            AnimalType.Zebra => "Z",
            AnimalType.Hippopotamus => "H",
            _ => "None"
        };
    }
}