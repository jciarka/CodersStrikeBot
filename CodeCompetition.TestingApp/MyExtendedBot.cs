using System;
using System.Linq;
using CodeStrikes.Sdk;
using System.Collections.Generic;
using System.Linq;
using CodeStrikes.Sdk.Bots;

namespace CodeStrikes.Sdk.Bots2
{
    public class FightingBot : BotBase
    {
        /// <summary>
        /// The Shaolin Master watches and remembers the opponent's past movements.
        /// Then, on their basis, he tries to estimate what movements will bring him the greatest benefits.
        /// The estimation is based on the heuristics implemented in the OptimalizationAwareMove class. 
        /// Heuristics give ranks to individual movements. Then the ranks are used to solve the knapsack problem
        /// in order to pick the best movement combination.
        /// </summary>

        // Fight state
        private RoundCounter roundCounter;
        private int myScoreTotal = 0;
        private int opponentScoreTotal = 0;
        private int energyPerRound = Config.EnergyPerRound;
        private List<RoundContext> battleContext = new List<RoundContext>();
        private Dictionary<Area, OptimalizationAwareMove> attackHistory;
        private Dictionary<Area, OptimalizationAwareMove> defenseHistory;

        private BestAtackDefenceWraper bestFromLastMove = new BestAtackDefenceWraper { bestAttackValid = false, bestDefenceValid = false };

        // KnapsackSolver
        private IKnapsackSolver<OptimalizationAwareMove> solver;

        public FightingBot()
        {
            roundCounter = new RoundCounter();

            attackHistory = new Dictionary<Area, OptimalizationAwareMove>
            {
                { Area.HookKick, new OptimalizationAwareMove( MoveType.Attack, Area.HookKick,
                                                              Config.AttacksBaseValues[Area.HookKick], roundCounter)  },
                { Area.HookPunch, new OptimalizationAwareMove( MoveType.Attack, Area.HookPunch,
                                                              Config.AttacksBaseValues[Area.HookPunch], roundCounter)  },
                { Area.LowKick, new OptimalizationAwareMove( MoveType.Attack,  Area.LowKick,
                                                              Config.AttacksBaseValues[Area.LowKick], roundCounter)  },
                { Area.UppercutPunch, new OptimalizationAwareMove( MoveType.Attack, Area.UppercutPunch,
                                                              Config.AttacksBaseValues[Area.UppercutPunch], roundCounter)  },
            };

            defenseHistory = new Dictionary<Area, OptimalizationAwareMove>
            {
                { Area.HookKick, new OptimalizationAwareMove( MoveType.Defense, Area.HookKick,
                                                              Config.DefensesBaseValues[Area.HookKick], roundCounter)  },
                { Area.HookPunch, new OptimalizationAwareMove( MoveType.Defense, Area.HookPunch,
                                                              Config.DefensesBaseValues[Area.HookPunch], roundCounter)  },
                { Area.LowKick, new OptimalizationAwareMove( MoveType.Defense,  Area.LowKick,
                                                              Config.DefensesBaseValues[ Area.LowKick], roundCounter)  },
                { Area.UppercutPunch, new OptimalizationAwareMove( MoveType.Defense, Area.UppercutPunch,
                                                              Config.DefensesBaseValues[Area.UppercutPunch], roundCounter)  },
            };

            solver = new DynamicKanpsnackSolver<OptimalizationAwareMove>();
        }

        public override MoveCollection NextMove(RoundContext context)
        {
            updateState(context);

            foreach (OptimalizationAwareMove defence in defenseHistory.Values)
            {
                defence.ClearPromotion();
                if (bestFromLastMove.bestDefenceValid && defence.Area == bestFromLastMove.bestDefence)
                {
                    defence.Promote();
                }
            }

            foreach (OptimalizationAwareMove attack in attackHistory.Values)
            {
                attack.ClearPromotion();
                if (bestFromLastMove.bestAttackValid && attack.Area == bestFromLastMove.bestAttack)
                {
                    attack.Promote();
                }
            }

            IEnumerable<OptimalizationAwareMove> nextMoves = solver.solve(defenseHistory.Values.Concat(attackHistory.Values),
                                                                          energyPerRound);

            // Set attacks and defences
            foreach (var move in nextMoves)
            {

                if (move.Type == MoveType.Attack)
                    context.MyMoves.AddAttack(move.Area);
                else
                    context.MyMoves.AddDefence(move.Area);
            }

            // <--
            if (context.LastOpponentMoves != null && context.MyMoves != null)
            {
                Console.WriteLine("My health = " + context.MyLifePoints.ToString());
                foreach (Move move in context.MyMoves?.GetAttacks())
                {
                    Console.WriteLine("my attack: " + move.Area.ToString());
                }
                foreach (Move move in context.MyMoves?.GetDefences())
                {
                    Console.WriteLine("my defence: " + move.Area.ToString());
                }

                Console.WriteLine("Oponents health = " + context.OpponentLifePoints.ToString());
                foreach (Move move in context.LastOpponentMoves?.GetAttacks())
                {
                    Console.WriteLine("oponent attack: " + move.Area.ToString());
                }
                foreach (Move move in context.LastOpponentMoves?.GetDefences())
                {
                    Console.WriteLine("oponent defence: " + move.Area.ToString());
                }
            }

            // <--


            return context.MyMoves;
        }

        private void updateState(RoundContext context)
        {
            roundCounter.nextRound();
            myScoreTotal += context.MyDamage;
            opponentScoreTotal += context.OpponentDamage;
            battleContext.Add(context);

            bestFromLastMove = new BestAtackDefenceWraper();

            if (context.LastOpponentMoves != null)
            {


                // ________ Best attack and defence from n-th moves ________
                var lastNthRounds = battleContext.Skip(Math.Max(0, battleContext.Count() - Config.numLastRoundsConsidered))
                            .Take(Config.numLastRoundsConsidered);

                Console.WriteLine("Rounds considered = " + lastNthRounds.Count());

                // clear history 
                foreach (var move in attackHistory.Keys)
                {
                    attackHistory[move].TimesPresent = 0;
                    defenseHistory[move].TimesPresent = 0;
                }

                //________ Count attacks from last n - th moves ________
                foreach (var round in lastNthRounds)
                {
                    // attacks
                    foreach (Move move in context.LastOpponentMoves?.GetDefences())
                    {
                        attackHistory[move.Area].TimesPresent += 1;
                    }
                    // defences
                    foreach (Move move in context.LastOpponentMoves?.GetAttacks())
                    {
                        defenseHistory[move.Area].TimesPresent += 1;
                    }
                }

                // ________ Best attack and defence from last move ________

                // best defenece from last move
                List<Area> defences = new List<Area> { Area.HookKick, Area.HookPunch, Area.LowKick, Area.LowKick };
                foreach (Move opponentMove in context.LastOpponentMoves?.GetDefences())
                {
                    defences.Remove(opponentMove.Area);
                }

                // Find best possible attack for previous defence
                if (defences.Count() > 0)
                {
                    foreach (Area notOpponentDefence in defences)
                    {
                        if (Config.AttacksBaseValues[notOpponentDefence] > Config.AttacksBaseValues[bestFromLastMove.bestAttack])
                        {
                            bestFromLastMove.bestAttack = notOpponentDefence;
                            bestFromLastMove.bestAttackValid = true;
                        }
                    }
                }
                else
                {
                    bestFromLastMove.bestAttackValid = false;
                }

                // best defence from last move
                foreach (Move opponentMove in context.LastOpponentMoves?.GetAttacks())
                {
                    if (Config.DefensesBaseValues[opponentMove.Area] > Config.DefensesBaseValues[bestFromLastMove.bestDefence])
                    {
                        bestFromLastMove.bestDefence = opponentMove.Area;
                        bestFromLastMove.bestDefenceValid = true;
                    }
                }
            }
        }

        public override string ToString()
        {
            return "Yoda Master";
        }
    }

    public class OptimalizationAwareMove : Move, IKnapsackItem
    {
        public int TimesPresent { get; set; }
        public int ScopePresent => Config.numLastRoundsConsidered;

        // IKnapsackItem value
        private double baseValue;

        public double Value => countCurrentValue();

        public double countCurrentValue()
        {
            if (this.Type == MoveType.Attack)
                return (1 + ScopePresent - TimesPresent) * baseValue * GetEnergy() * promotion;
            else
                return (1 + TimesPresent) * baseValue * GetEnergy() * promotion;
        }

        // IKnapsackItem weight
        public int Weight => base.GetEnergy();

        // IKnapsackItem quantity
        public int Quantity { get; private set; }

        // Round number counter
        private RoundCounter roundCounter;

        private int promotion { get; set; } = 1;

        public OptimalizationAwareMove(MoveType type, Area area, double baseValue, RoundCounter globalRoundCounter, int maxAttackTypeRepetitions = 2)
            : base(type, area)
        {
            this.baseValue = baseValue;

            if (type == MoveType.Attack)
                // You can repeat one attack
                // as may times as you wish
                Quantity = maxAttackTypeRepetitions;
            else
                // You should not repeat defences
                Quantity = 1;

            this.roundCounter = globalRoundCounter;
        }

        public void Promote()
        {
            this.promotion = Config.promotionValue;
        }

        public void ClearPromotion()
        {
            this.promotion = 1;
        }
    }

    public class BestAtackDefenceWraper
    {
        public Area bestAttack;
        public bool bestAttackValid = false;
        public Area bestDefence;
        public bool bestDefenceValid = false;
    }

    public interface IKnapsackItem
    {
        int Weight { get; }
        double Value { get; }
        int Quantity { get; }
    }

    public interface IKnapsackSolver<T> where T : IKnapsackItem
    {
        IEnumerable<T> solve(IEnumerable<T> itemsList, int maxWeight);
    }

    public static class Config
    {
        public static Dictionary<Area, double> AttacksBaseValues { get; } = new Dictionary<Area, double>
        {
            { Area.HookKick, 11.0/4.0 },
            { Area.HookPunch, 6.0/3.0 },
            { Area.UppercutPunch, 3.5/2.0 },
            { Area.LowKick, 0.2/1.0 },
        };

        public static Dictionary<Area, double> DefensesBaseValues { get; } = new Dictionary<Area, double>
        {
            { Area.HookKick, 12.0/4.0 },
            { Area.HookPunch, 6.0/4.0 },
            { Area.UppercutPunch, 3.5/4.0 },
            { Area.LowKick, 0.2/4.0 },
        };

        public static int EnergyPerRound { get; } = 12;
        public static int numLastRoundsConsidered = 10;
        public static int promotionValue = 2;
    }

    public class DynamicKanpsnackSolver<T> : IKnapsackSolver<T> where T : IKnapsackItem
    {
        public IEnumerable<T> solve(IEnumerable<T> itemsList, int maxWeight)
        {
            List<T> items = new List<T>();
            // transformation 0-1 kancpsnack problem
            foreach (T item in itemsList)
            {
                for (int k = 0;
                    k < (item.Quantity > maxWeight / item.Weight ?
                         maxWeight / item.Weight : item.Quantity);
                    k++)

                    items.Add(item);
            }

            double[,] results = new double[items.Count + 1, maxWeight + 1];
            bool[,,] isContained = new bool[items.Count, items.Count + 1, maxWeight + 1];


            for (int i = 0; i <= items.Count; i++)
            {
                for (int w = 0; w <= maxWeight; w++)
                {
                    if (i == 0 || w == 0)
                    {
                        results[i, w] = 0;
                        for (int k = 0; k < items.Count; k++)
                            isContained[k, i, w] = false;
                    }
                    else if (items[i - 1].Weight <= w)
                    {
                        double valWithNoElem = results[i - 1, w];
                        double valWithElem = items[i - 1].Value + results[i - 1, w - items[i - 1].Weight];

                        if (valWithNoElem > valWithElem)
                        {
                            results[i, w] = valWithNoElem;
                            for (int k = 0; k < items.Count; k++)
                                isContained[k, i, w] = isContained[k, i - 1, w];
                        }
                        else
                        {
                            results[i, w] = valWithElem;
                            for (int k = 0; k < items.Count; k++)
                            {
                                if (k != i - 1)
                                    isContained[k, i, w] = isContained[k, i - 1, w - items[i - 1].Weight];
                                else
                                    isContained[k, i, w] = true;
                            }
                        }
                    }
                    else
                    {
                        results[i, w] = results[i - 1, w];
                        for (int k = 0; k < items.Count; k++)
                            isContained[k, i, w] = isContained[k, i - 1, w];
                    }
                }
            }

            List<T> resultList = new List<T>();
            for (int k = 0; k < items.Count; k++)
            {
                if (isContained[k, items.Count, maxWeight])
                    resultList.Add(items[k]);
            }

            return resultList;
        }
    }

    public class RoundCounter
    {
        public int roundNumber { get; private set; } = 0;

        public void nextRound()
        {
            roundNumber++;
        }

    }

}