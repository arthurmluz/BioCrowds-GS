using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq;

public class Poptime {

    private static readonly System.Random rng = new System.Random(42);
    private const int RoundingPrecision = 3;

    private const int ServiceTime = 1;     // in hours
    private static int AmountOfPlaces = 0;
    private static readonly List<float> TimeDistanceOrigins = new List<float> {};
    private static readonly List<double> PeoplePerOrigin = new List<double>();
    
    // poptime desejado
    private static readonly List<(int Hour, int Count)> FunctionalHours = new List<(int, int)> {
            (8, 85)//, (9, 100), (10, 91), (11, 64), (12, 33), (13, 33)
     };

    public static void calculaPoptimes() {
        if (TimeDistanceOrigins.Count == 0) {
            Debug.Log("No TimeDistanceOrigins found");
            return;
        }
        AmountOfPlaces = TimeDistanceOrigins.Count;

        double initialPeople = 0.0;

        for (int idx = 0; idx < FunctionalHours.Count; idx++) {
            var (hour, count) = FunctionalHours[idx];

            if (initialPeople == 0.0) {
                initialPeople = GetInitialPeople(count);
                PrintResults(hour, initialPeople);
                continue;
            }

            // People leaving from ServiceTime hours ago
            double peopleLeaving = 0.0;
            if (idx - ServiceTime >= 0) {
                var (_, prevCount) = FunctionalHours[idx - ServiceTime];
                peopleLeaving = GetPeoplePerTime(prevCount);
            }

            // People arriving in this hour (delta plus anyone still in transit)
            var (_, lastCount) = FunctionalHours[idx - 1];
            double arriving = GetPeoplePerTime(count - lastCount, peopleLeaving);
            PrintResults(hour, arriving);
        }
    }

    private static void PrintResults(int hour, double arriving) {
        Debug.Log($"From: {hour - 1} to {hour} there are {System.Math.Round(arriving, RoundingPrecision)} people arriving");
        PeoplePerOrigin.Clear();
        DividePeopleByOrigins(arriving, AmountOfPlaces);
        //PrettyPrintPeoplePerOrigin();
    }

    private static void DividePeopleByOrigins(double totalPeople, int places) {
        // Base two-way split
        double n1 = System.Math.Round(rng.NextDouble() * (totalPeople - 0.1) + 0.1, RoundingPrecision);
        double n2 = System.Math.Round(totalPeople - n1, RoundingPrecision);

        if (n1 < 0.01) {
            DividePeopleByOrigins(totalPeople, places);
        }

        PeoplePerOrigin.Add(n1);
        PeoplePerOrigin.Add(n2);

        if (PeoplePerOrigin.Count >= places)
            return;

        // Take the largest chunk and split it further
        double maxChunk = PeoplePerOrigin.Max();
        PeoplePerOrigin.Remove(maxChunk);
        DividePeopleByOrigins(maxChunk, places);
    }

    private static void PrettyPrintPeoplePerOrigin() {
        for (int i = 0; i < PeoplePerOrigin.Count; i++)
        {
            double people = PeoplePerOrigin[i];
            float travelTime = TimeDistanceOrigins[i];
            double departing = System.Math.Round(people * travelTime, RoundingPrecision);
            Debug.Log($" --- amount of people leaving from place {i + 1} per time: {departing} \t: taking {travelTime} to reach their destination");
        }
    }

    private static double GetPeoplePerTime(int deltaCount, double carryOver = 0.0) {
        double perMinute = System.Math.Round(deltaCount / 60.0, RoundingPrecision);
        return perMinute + carryOver;
    }

    private static double GetInitialPeople(int count) {
        // People take 1 hour to arrive at the first time
        return System.Math.Round(count / 60.0, RoundingPrecision);
    }

    public static void addTimeDistanceOrigin(float origin) {
        TimeDistanceOrigins.Add(origin);
    }

    public static float getTimeDistanceOriginByDistance(float distance) {
        if (TimeDistanceOrigins.Count == 0) {
            Debug.Log("No TimeDistanceOrigins found");
            return 0.0f;
        }

        // Find the closest time distance origin to the given distance
        float closest = TimeDistanceOrigins.OrderBy(x => Mathf.Abs(x - distance)).FirstOrDefault();
        // 1 person per time unit
        Debug.Log($"Cycle length: {1/closest}");
        return (1/closest) * 10.0f; 
    }

    public static void clear() {
        PeoplePerOrigin.Clear();
        TimeDistanceOrigins.Clear();
        AmountOfPlaces = 0;
    }
}