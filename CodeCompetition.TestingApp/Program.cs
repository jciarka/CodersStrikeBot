using System;
using CodeStrikes.Sdk;
using CodeStrikes.Sdk.Bots;

namespace CodeStrikes.TestingApp
{
    class Program
    {
        static void Main(string[] args)
        {
            CodeStrikes.Sdk.Bots1.FightingBot oldBot = new CodeStrikes.Sdk.Bots1.FightingBot();
            CodeStrikes.Sdk.Bots2.FightingBot newBot = new CodeStrikes.Sdk.Bots2.FightingBot();
            PlayerBot playerBot = new PlayerBot();
            Kickboxer kickboxer = new Kickboxer();
            Boxer boxer = new Boxer();


            Console.WriteLine($"Executing fight: {newBot} vs {oldBot}");
            Fight fight = new Fight(newBot, oldBot, new StandardGameLogic());
            var result = fight.Execute();
            // Uncomment to see round results
            // result.RoundResults.ForEach(Console.WriteLine);
            Console.WriteLine($"Result: {result}");
            Console.WriteLine();

            Console.WriteLine($"Executing fight: {oldBot} vs {newBot}");
            fight = new Fight(oldBot, newBot, new StandardGameLogic());
            result = fight.Execute();
            // Uncomment to see round results
            //result.RoundResults.ForEach(Console.WriteLine);
            Console.WriteLine($"Result: {result}");

            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
