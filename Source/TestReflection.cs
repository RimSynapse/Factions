using System;
using System.Reflection;
using RimWorld;

public class TestReflection
{
    public static void Run()
    {
        Type t = typeof(RimWorld.Pawn_GuestTracker);
        Console.WriteLine("Type: " + t.Name);
        foreach (var p in t.GetFields()) Console.WriteLine("Field: " + p.Name);
        foreach (var p in t.GetProperties()) Console.WriteLine("Property: " + p.Name);
    }
}
