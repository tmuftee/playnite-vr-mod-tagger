using System;
using System.IO;

namespace VrModTagger.Tests
{
    internal static class Fixture
    {
        // Saved copies of the real source pages, taken 2026-10-28.
        public static string Read(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name));
        }
    }
}
