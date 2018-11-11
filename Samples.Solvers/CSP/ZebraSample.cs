/*==============================================================================
// Copyright © Microsoft Corporation.  All Rights Reserved.
// This code released under the terms of the 
// Microsoft Public License (MS-PL, http://opensource.org/licenses/ms-pl.html.)
==============================================================================*/

///----------------- User Guide Sample 2: Zebra -------------------------------
/// <summary> Who drinks water and who keeps a zebra?
/// </summary>
/// 
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.SolverFoundation.Common;
using Microsoft.SolverFoundation.Solvers;

namespace Samples.Solvers.CSP
{

    public class ZebraSample
    {


        delegate CspTerm NamedTerm(string name);


        public static void Run()
        {

            ConstraintSystem S = ConstraintSystem.CreateSolver();

            List<KeyValuePair<CspTerm, string>> termList = new List<KeyValuePair<CspTerm, string>>();

            // create a Term between [1..5], associate it with a name for later ease of display

            NamedTerm namedTerm = delegate (string name) {
                CspTerm x = S.CreateVariable(S.CreateIntegerInterval(1, 5), name);
                termList.Add(new KeyValuePair<CspTerm, string>(x, name));
                return x;
            };

            // the people and attributes will all be matched via the house they reside in.

            CspTerm English = namedTerm("English"), Spanish = namedTerm("Spanish"), Japanese = namedTerm("Japanese"), Italian = namedTerm("Italian"), Norwegian = namedTerm("Norwegian");
            CspTerm red = namedTerm("red"), green = namedTerm("green"), white = namedTerm("white"), blue = namedTerm("blue"), yellow = namedTerm("yellow");
            CspTerm dog = namedTerm("dog"), snails = namedTerm("snails"), fox = namedTerm("fox"), horse = namedTerm("horse"), zebra = namedTerm("zebra");
            CspTerm painter = namedTerm("painter"), sculptor = namedTerm("sculptor"), diplomat = namedTerm("diplomat"), violinist = namedTerm("violinist"), doctor = namedTerm("doctor");
            CspTerm tea = namedTerm("tea"), coffee = namedTerm("coffee"), milk = namedTerm("milk"), juice = namedTerm("juice"), water = namedTerm("water");

            S.AddConstraints(
              S.Unequal(English, Spanish, Japanese, Italian, Norwegian),
              S.Unequal(red, green, white, blue, yellow),
              S.Unequal(dog, snails, fox, horse, zebra),
              S.Unequal(painter, sculptor, diplomat, violinist, doctor),
              S.Unequal(tea, coffee, milk, juice, water),
              S.Equal(English, red),
              S.Equal(Spanish, dog),
              S.Equal(Japanese, painter),
              S.Equal(Italian, tea),
              S.Equal(1, Norwegian),
              S.Equal(green, coffee),
              S.Equal(1, green - white),
              S.Equal(sculptor, snails),
              S.Equal(diplomat, yellow),
              S.Equal(3, milk),
              S.Equal(1, S.Abs(Norwegian - blue)),
              S.Equal(violinist, juice),
              S.Equal(1, S.Abs(fox - doctor)),
              S.Equal(1, S.Abs(horse - diplomat))
            );

            bool unsolved = true;
            ConstraintSolverSolution soln = S.Solve();
            while (soln.HasFoundSolution)
            {
                unsolved = false;
                System.Console.WriteLine("solved.");
                StringBuilder[] houses = new StringBuilder[5];
                for (int i = 0; i < 5; i++)
                    houses[i] = new StringBuilder(i.ToString());
                foreach (KeyValuePair<CspTerm, string> kvp in termList)
                {
                    string item = kvp.Value;
                    object house;
                    if (!soln.TryGetValue(kvp.Key, out house))
                        throw new InvalidProgramException("can't find a Term in the solution: " + item);
                    houses[(int)house - 1].Append(", ");
                    houses[(int)house - 1].Append(item);
                }
                foreach (StringBuilder house in houses)
                {
                    System.Console.WriteLine(house);
                }
                soln.GetNext();
            }
            if (unsolved)
                System.Console.WriteLine("No solution found.");
            else
                System.Console.WriteLine("Solution should have the Norwegian drinking water and the Japanese with the zebra.");
        }

        //public static void Main(string[] args)
        //{
        //    ZebraSample.Zebra();
        //}
    }
}

