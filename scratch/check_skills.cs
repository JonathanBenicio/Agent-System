using System;
using System.Reflection;

class Program {
    static void Main() {
        try {
            var asm = Assembly.Load("Microsoft.Agents.AI");
            foreach (var t in asm.GetTypes()) {
                if (t.Name.Contains("Skill")) {
                    Console.WriteLine(t.FullName);
                }
            }
            Console.WriteLine("Done");
        } catch(Exception e) { Console.WriteLine(e); }
    }
}
