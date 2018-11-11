using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SolverFoundation.Solvers;
using Microsoft.SolverFoundation.Services;

namespace Samples.Solvers.CQN
{
    public class Rosenbrock
    {
        public static void SolveRosenbrock()
        {
            var solverParams = new CompactQuasiNewtonSolverParams();
            CompactQuasiNewtonSolver solver = new CompactQuasiNewtonSolver();
            int vidRow, vidVariableX, vidVariableY;
            //add variables
            solver.AddVariable(key: null, vid: out vidVariableX);
            solver.AddVariable(key: null, vid: out vidVariableY);
            //add a row and set it as the goal
            solver.AddRow(key: null, vid: out vidRow);
            solver.AddGoal(vid: vidRow, pri: 0, minimize: true);
            solver.FunctionEvaluator = OriginalRosenbrockFunction;
            solver.GradientEvaluator = OriginalRosenbrockGradient;
            solver.Solve(solverParams);
            Console.WriteLine("=========Original Rosenbrock==========");
            Console.WriteLine(solver.ToString());
        }

        public static void SolveFirstMultidimensionalVariant(int dimentions)
        {
            var solverParams = new CompactQuasiNewtonSolverParams();
            var solver = new CompactQuasiNewtonSolver();
            int vidRow;
            int[] vidVariables = new int[dimentions];
            //add variables
            for (int i = 0; i < dimentions; i++)
                solver.AddVariable(null, out vidVariables[i]);
            //add a row and set it as the goal
            solver.AddRow(null, out vidRow);
            solver.AddGoal(vidRow, 0, true);

            // let's try some non-default starting point
            for (int i = 0; i < dimentions; i++)
                solver.SetValue(vidVariables[i], -10);
            solver.FunctionEvaluator = FirstRosenbrockVariantFunction;
            solver.GradientEvaluator = FirstRosenbrockVariantGradient;
            //Solve the model
            solver.Solve(solverParams);
            Console.WriteLine("=========First multidimensional variant of Rosenbrock (many uncoupled 2D Rosenbrock problems)==========");
            Console.WriteLine(solver.ToString());

            // let's limit the number of iteration to 5 less than actually needed. 
            // We might get close enough answer.
            solverParams.IterationLimit = solver.IterationCount - 5;
            // set the starting point again
            for (int i = 0; i < dimentions; i++)
                solver.SetValue(vidVariables[i], -10);

            //Solve the model
            solver.Solve(solverParams);
            Console.WriteLine("=========First multidimensional variant of Rosenbrock, MaxIteration exceeded==========");
            Console.WriteLine(solver.ToString());
        }

        public static void SolveSecondMultidimensionalVariant(int dimentions)
        {
            var solverParams = new CompactQuasiNewtonSolverParams();
            var solver = new CompactQuasiNewtonSolver();
            int vidRow;
            int[] vidVariables = new int[dimentions];
            //add variables
            for (int i = 0; i < dimentions; i++)
                solver.AddVariable(null, out vidVariables[i]);
            //add a row and set it as the goal
            solver.AddRow(null, out vidRow);
            solver.AddGoal(vidRow, 0, true);
            solver.FunctionEvaluator = SecondRosenbrockVariantFunction;
            solver.GradientEvaluator = SecondRosenbrockVariantGradient;
            solver.Solve(solverParams);
            Console.WriteLine("=========Second, more complicated multidimensional variant of Rosenbrock==========");
            Console.WriteLine(solver.ToString());

            // This variant of Rosenbrock function has a local minima as well 
            // around the point x = -1, 1, 1, .... (first dimension is around -1 and the rest are around 1)
            // If we start from around this local optima the solver will find and return it, and may not get
            // to the global optima
            solver.SetValue(vidVariables[0], -1);
            for (int i = 1; i < dimentions; i++)
                solver.SetValue(vidVariables[i], 1);
            //Solve the model
            solver.Solve(solverParams);
            Console.WriteLine("=========Second, more complicated multidimensional variant of Rosenbrock, trapped in local Minima==========");
            Console.WriteLine(solver.ToString());
        }

        /// <summary>
        /// Function value callback for the original Rosenbrock's function
        /// f(x, y) = (1 - x)^2 + 100(y - x^2)^2  
        /// <param name="model">the model.</param>
        /// <param name="rowVid">the row index.</param>
        /// <param name="values">the variable values.</param>
        /// <param name="newValues">is first evaluator call with those variable values.</param>
        /// <returns>the row value</returns>
        private static double OriginalRosenbrockFunction(INonlinearModel model, int rowVid, ValuesByIndex values, bool newValues)
        {
            double value = Math.Pow(1 - values[1], 2) + 100 * (Math.Pow(values[2] - (values[1] * values[1]), 2));
            return value;
        }

        /// <summary>
        /// Gradient value callback for the original Rosenbrock's function
        /// f(x, y) = (1 - x)^2 + 100(y - x^2)^2  
        /// </summary>
        /// <param name="model">the model.</param>
        /// <param name="rowVid">the row index.</param>
        /// <param name="values">the variable values.</param>
        /// <param name="newValues">is first evaluator call with those variable values.</param>
        /// <param name="gradient">the gradient values (set by the user).</param>
        private static void OriginalRosenbrockGradient(INonlinearModel model, int rowVid, ValuesByIndex values, bool newValues, ValuesByIndex gradient)
        {
            gradient[1] = -2 * (1 - values[1]) - 400 * values[1] * (values[2] - (values[1] * values[1]));
            gradient[2] = 200 * (values[2] - (values[1] * values[1]));
        }

        /// <summary>
        /// Function value callback for first variant of Rosenbrock's function.
        /// This is the multidimensional variant of Ronsenbrock when n must be pair
        /// f(x) = sum(i, from 1 to N){ [alpha(x(2i) - x(2i-1)^2)^2] + [(1 - x(2i-1))^2] }
        /// <param name="model">the model.</param>
        /// <param name="rowVid">the row index.</param>
        /// <param name="values">the variable values.</param>
        /// <param name="newValues">is first evaluator call with those variable values.</param>
        /// <returns>the row value</returns>
        private static double FirstRosenbrockVariantFunction(INonlinearModel model, int rowVid, ValuesByIndex values, bool newValues)
        {
            const int firstVid = 1;
            const int alpha = 100;
            double value = 0;
            int dimentions = model.VariableCount;
            if (dimentions < 2)
                throw new ArgumentException("Multidimensional variant require at least two dimensions");
            for (int i = firstVid; i <= dimentions / 2; i++)
            {
                value += alpha * (Math.Pow((values[2 * i] - values[2 * i - 1] * values[2 * i - 1]), 2)) + (Math.Pow(1 - values[2 * i - 1], 2));
            }
            return value;
        }

        /// <summary>
        /// Gradient value callback for the first variant of Rosenbrock's function.
        /// This is the multidimensional variant of Ronsenbrock when n must be pair
        /// f(x) = sum(i, from 1 to N){ [alpha(x(2i) - x(2i-1)^2)^2] + [(1 - x(2i-1))^2] }
        /// </summary>
        /// <param name="model">the model.</param>
        /// <param name="rowVid">the row index.</param>
        /// <param name="values">the variable values.</param>
        /// <param name="newValues">is first evaluator call with those variable values.</param>
        /// <param name="gradient">the gradient values (set by the user).</param>
        private static void FirstRosenbrockVariantGradient(INonlinearModel model, int rowVid, ValuesByIndex values, bool newValues, ValuesByIndex gradient)
        {
            const int firstVid = 1;
            const int alpha = 100;
            int dimentions = model.VariableCount;
            if (dimentions < 2)
                throw new ArgumentException("Multidimensional variant require at least two dimensions");
            for (int i = firstVid; i <= dimentions / 2; i++)
            {
                gradient[2 * i - 1] = (4 * alpha * values[2 * i - 1] * (values[2 * i - 1] * values[2 * i - 1] - values[2 * i]) + 2 * values[2 * i - 1] - 2);
                gradient[2 * i] = (2 * alpha * -1 * (values[2 * i - 1] * values[2 * i - 1] - values[2 * i]));
            }
        }

        /// <summary>
        /// Function value callback for the second multidimensional variant of Rosenbrock's function
        /// f(x) = sum(i, from 1 to N-1){ [alpha(x(i+1) - x(i)^2)^2] + [(1 - x(i))^2] }
        /// <param name="model">the model.</param>
        /// <param name="rowVid">the row index.</param>
        /// <param name="values">the variable values.</param>
        /// <param name="newValues">is first evaluator call with those variable values.</param>
        /// <returns>the row value</returns>
        private static double SecondRosenbrockVariantFunction(INonlinearModel model, int rowVid, ValuesByIndex values, bool newValues)
        {
            const int firstVid = 1;
            const int alpha = 100;
            int dimentions = model.VariableCount;
            if (dimentions < 2)
                throw new ArgumentException("Multidimensional variant require at least two dimensions");

            // first dimention special case
            double value = alpha * (Math.Pow((values[firstVid + 1] - values[firstVid] * values[firstVid]), 2)) +
                           (Math.Pow(1 - values[firstVid], 2));

            for (int i = firstVid + 1; i < dimentions; i++)
            {
                value += alpha * (Math.Pow((values[i + 1] - values[i] * values[i]), 2)) + (Math.Pow(1 - values[i], 2));

            }
            return value;
        }

        /// <summary>
        /// Gradient value callback for second multidimensional variant of Rosenbrock's function
        /// f(x) = sum(i, from 1 to N-1){ [alpha(x(i+1) - x(i)^2)^2] + [(1 - x(i))^2] }
        /// </summary>
        /// <param name="model">the model.</param>
        /// <param name="rowVid">the row index.</param>
        /// <param name="values">the variable values.</param>
        /// <param name="newValues">is first evaluator call with those variable values.</param>
        /// <param name="gradient">the gradient values (set by the user).</param>
        private static void SecondRosenbrockVariantGradient(INonlinearModel model, int rowVid, ValuesByIndex values, bool newValues, ValuesByIndex gradient)
        {
            const int firstVid = 1;
            // common alpha is 100
            int alpha = 100;
            int dimentions = model.VariableCount;
            if (dimentions < 2)
                throw new ArgumentException("Multidimensional variant require at least two dimensions");
            // first dimention special case
            gradient[firstVid] = 4 * alpha * values[firstVid] * (values[firstVid] * values[firstVid] - values[firstVid + 1]) +
                          2 * (values[firstVid] - 1);
            for (int i = firstVid + 1; i < dimentions; i++)
            {
                gradient[i] = -2 * alpha * (values[i - 1] * values[i - 1] - values[i]) +
                               4 * alpha * values[i] * (values[i] * values[i] - values[i + 1]) +
                               2 * (values[i] - 1);
            }
            // last dimention special case
            gradient[dimentions] = -2 * alpha * (values[dimentions - 1] * values[dimentions - 1] - values[dimentions]);
        }
    }
}
