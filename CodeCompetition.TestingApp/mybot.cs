using System;
using System.Linq;
using CodeStrikes.Sdk;
using System.Collections.Generic;
using System.Linq;
using CodeStrikes.Sdk.Bots;

namespace CodeStrikes.Sdk.Bots1
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

            foreach(OptimalizationAwareMove defence in defenseHistory.Values)
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
                if(bestFromLastMove.bestAttackValid && attack.Area == bestFromLastMove.bestAttack)
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

            return context.MyMoves;
        }

        private void updateState(RoundContext context)
        {
            roundCounter.nextRound();
            myScoreTotal += context.MyDamage;
            opponentScoreTotal += context.OpponentDamage;

            bestFromLastMove = new BestAtackDefenceWraper();

            if (context.LastOpponentMoves != null)
            {
                List<Area> defences = new List<Area>{ Area.HookKick, Area.HookPunch, Area.LowKick, Area.LowKick };

                foreach (Move opponentMove in context.LastOpponentMoves?.GetDefences())
                {
                    attackHistory[opponentMove.Area].IncOpponentCountermovementsQuantity();
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


                foreach (Move opponentMove in context.LastOpponentMoves?.GetAttacks())
                {
                    defenseHistory[opponentMove.Area].IncOpponentCountermovementsQuantity();
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
            return "Shaolin Master";
        }
    }

    public class BestAtackDefenceWraper
    {
        public Area bestAttack;
        public bool bestAttackValid = false;
        public Area  bestDefence;
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


    public class OptimalizationAwareMove : Move, IKnapsackItem
    {
        /// <summary>
        /// Class represents move and provides addditional information required by knapsnac solver
        /// Class also stores statistics about the corresponding opponent's movement. 
        /// For example, if a class describes a Hook Kick attack, the class will keep information about 
        /// the number of opponent's defenses for that attack in the entire fight
        /// </summary>


        // IKnapsackItem value
        private double baseValue;
        public double Value => countCurrentValue();

        /// <summary>
        /// The heuristics computes the rank of an attack as follows. The base value is multiplied
        /// by the number of possible successful attacks (number of rounds when the opponent was not defending them)
        /// and the energy needed. The last element is needed to keep the right balance between the different movements.
        /// When a class describes defense, the number of corresponding attachs is taken into account
        /// instead of the number of opponents' defenses
        /// </summary>
        public double countCurrentValue()
        {
            if (this.Type == MoveType.Attack)
                return (roundCounter.roundNumber - OpponentCountermovementsQuantity) * baseValue * GetEnergy() * promotion;
            else
                return (1 + OpponentCountermovementsQuantity) * baseValue * GetEnergy() * promotion;
        }

        // IKnapsackItem weight
        public int Weight => base.GetEnergy();

        // IKnapsackItem quantity
        public int Quantity { get; private set; }

        // Countermovements counter for value calculations
        public int OpponentCountermovementsQuantity { get; private set; } = 0;
        public void IncOpponentCountermovementsQuantity() => OpponentCountermovementsQuantity++;

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
            this.promotion = roundCounter.roundNumber;
        }

        public void ClearPromotion()
        {
            this.promotion = 1;
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