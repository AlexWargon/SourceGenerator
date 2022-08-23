using System;

namespace App {
    internal static class Program {
        private static void Main(string[] args) {
            var systems = new Systems();
            systems.Update();
            var str = "//LAMBDA_DEPTH:0,ID:1";
            Console.WriteLine(str.Contains("LAMBDA_DEPTH"));
            var index = str.IndexOf("LAMBDA_DEPTH:", StringComparison.Ordinal);
            Console.WriteLine(index);
            var depth = int.Parse($"{str[index + 13]}");
            var id = int.Parse($"{str[index + 18]}");
            Console.WriteLine($"DEAPTH = {depth}, ID = {id}");
        }
    }
}