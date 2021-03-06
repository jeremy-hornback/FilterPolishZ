﻿using FilterCore.Line;
using FilterExo.Model;
using FilterPolishUtil;
using FilterPolishUtil.Extensions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FilterExo.Core.PreProcess.Commands
{
    public class ExoExpressionCommand
    {
        public ExoExpressionCommand(List<string> values)
        {
            foreach (var item in values)
            {
                this.Values.Add(new ExoAtom(item));
            }
        }

        public ExoExpressionCommand(List<ExoAtom> values)
        {
            this.Values.AddRange(values);
        }

        public ExoBlock Parent { get; set; }

        public List<ExoAtom> Values = new List<ExoAtom>();

        public List<string> Serialize()
        {
            var resultingExpression = this.ResolveExpression();

            var results = new List<string>();
            foreach (var item in resultingExpression)
            {
                // here we attempt to resolve every value using the parents variables.
                // if no values will be found, we just return the basic value.
                results.Add(item.Serialize(this.Parent));
            }

            return results;
        }

        public List<ExoAtom> ResolveExpression()
        {
            var results = new List<ExoAtom>();

            // create bracket tree
            var tree = this.CreateIndentationTree(this.Values);

            ResolveBranch(tree.Tree);
            CombineResults(tree.Tree);

            // perform actions starting with deepest child
            void ResolveBranch(Branch<ExoAtom> branch)
            {
                foreach (var item in branch.Leaves)
                {
                    ResolveVariables(item);
                    ResolveBranch(item); // Recurse
                }

                if (branch.Leaves.Count != 0)
                {
                    // expression evaluation happens here
                    branch.Leaves = ResolveBranchExpression(branch.Leaves);
                }
            }

            // top down gather results
            void CombineResults(Branch<ExoAtom> branch)
            {
                TraceUtility.Check(branch.Content != null && branch.Leaves.Count > 0, "Branch has both content and leaves!");

                if (branch.Content != null)
                {
                    results.Add(branch.Content);
                }
                else
                {
                    foreach (var item in branch.Leaves)
                    {
                        // recurse
                        CombineResults(item);
                    }
                }

            }

            return results;
        }

        private void ResolveVariables(Branch<ExoAtom> item)
        {
            if (item.Content != null && item.Content.IdentifiedType == ExoAtomType.prim)
            {
                // when dealing with variables, we resolve them and if resolvement was succesfull
                // we put resolved single varibales into content and a list of values into leaves
                var resolved = item.Content.Resolve(this.Parent);

                if (resolved.Any())
                {
                    var resolvedList = resolved.ToList();
                    if (resolvedList.Count == 1)
                    {
                        item.Content = resolvedList[0];
                    }
                    else
                    {
                        item.Content = null;
                        resolvedList.ForEach(x => item.Leaves.Add(new Branch<ExoAtom>() { Content = x }));
                    }
                }
            }
        }

        private List<Branch<ExoAtom>> ResolveBranchExpression(List<Branch<ExoAtom>> children)
        {
            // Split by ,
            var splitChildren = children.SplitDivide(x => x.Content?.GetRawValue() == ",");
            var results = new List<Branch<ExoAtom>>();

            foreach (var subexpression in splitChildren)
            {
                // Detects potential functions and their parameters and then executes them, by replacing the 
                // Atoms with the resolved results
                ExecuteFunctions(subexpression);

                // get all children on the level we're working on
                var wipBranch = FlattenBranch(subexpression);

                // check the expressions for combinable patterns. Restart every time we find one.
                wipBranch = CombinePatterns(wipBranch);

                if (results.Count > 0)
                {
                    results.Add(new Branch<ExoAtom>() { Content = new ExoAtom(",") });
                }

                results.AddRange(wipBranch.Select(x => new Branch<ExoAtom>() { Content = x }).ToList());
            }

            return results;
        }

        private void ExecuteFunctions(List<Branch<ExoAtom>> subexpression)
        {
            bool funcMode = false;
            ExoAtom funcLink = null;
            var totalCount = subexpression.Count;
            for (int i = 0; i < totalCount; i++)
            {
                Branch<ExoAtom> branch = subexpression[i];
                if (funcMode)
                {
                    var function = funcLink.GetFunction(this.Parent);
                    var funcRes = function.Execute(branch, this).ToList();

                    if (funcRes.Count > 1)
                    {
                        if (subexpression.Count == 2)
                        {
                            subexpression.RemoveAt(i);
                            subexpression.RemoveAt(i - 1);

                            i = totalCount;

                            foreach (var item in funcRes)
                            {
                                this.Parent.AddCommand(new ExoExpressionCommand(item));
                            }
                        }
                    }
                    else
                    {
                        subexpression.RemoveAt(i);
                        subexpression.RemoveAt(i - 1);
                        subexpression.Add(funcRes[0].ToBranch());
                        totalCount = subexpression.Count;
                        i--;
                    }
                }

                if (branch.Content?.IdentifiedType == ExoAtomType.func)
                {
                    funcLink = branch.Content;
                    funcMode = true;
                }
                else
                {
                    funcMode = false;
                }
            }
        }

        // resolve expression. cancel and restart every time we resolved something
        // this way we handle the results of the expressions as a new variable
        private List<ExoAtom> CombinePatterns(List<ExoAtom> wipBranch)
        {
            bool success = true;
            while (success)
            {
                var combiner = new ExoExpressionCombineBuilder(this.Parent);
                success = false;

                for (int i = 0; i < wipBranch.Count; i++)
                {
                    success = combiner.Add(wipBranch[i]);

                    if (success)
                    {
                        combiner.Results.AddRange(wipBranch.Skip(i + 1));
                        wipBranch = combiner.Results;
                        break;
                    }
                }

                if (!success)
                {
                    success = combiner.Finish();
                    wipBranch = combiner.Results;
                }
            }

            return wipBranch;
        }

        public static List<ExoAtom> FlattenBranch(List<Branch<ExoAtom>> subexpression)
        {
            var results = new List<ExoAtom>();
            foreach (var leaf in subexpression)
            {
                if (leaf.Content == null)
                {
                    leaf.YieldAllContentBranches().ForEach(x => results.Add(x));
                }
                else
                {
                    results.Add(leaf.Content);
                }
            }

            return results;
        }

        // Time to get cerial
        public string SerializeDebug()
        {
            var serializedCommand = this.Serialize();
            var line = serializedCommand.ToFilterLine();
            return line.Serialize();
        }

        public void SetParent(ExoBlock parent)
        {
            this.Parent = parent;
        }

        public BracesTree<ExoAtom> CreateIndentationTree(List<ExoAtom> list)
        {
            var tree = new BracesTree<ExoAtom>();
            var parent = new Branch<ExoAtom>();
            var child = new Branch<ExoAtom>();

            foreach (var item in list)
            {
                if (item.IdentifiedType == ExoAtomType.oper && item.GetRawValue() == "(")
                {
                    child = new Branch<ExoAtom>() { };
                    parent.Leaves.Add(child);

                    tree.Stack.Push(parent);
                    parent = child;
                }
                else if (item.IdentifiedType == ExoAtomType.oper && item.GetRawValue() == ")")
                {
                    child = parent;
                    parent = tree.Stack.Pop();
                }
                else
                {
                    child = new Branch<ExoAtom>() { Content = item };
                    parent.Leaves.Add(child);
                }
            }

            tree.Tree = parent;

            return tree;
        }
    }

    public class BracesTree<T>
    {
        public Stack<Branch<T>> Stack = new Stack<Branch<T>>();
        public Branch<T> Tree = new Branch<T>();
    }

    public class Branch<T>
    {
        public Branch<T> Parent;
        public List<Branch<T>> Leaves = new List<Branch<T>>();
        public T Content;

        public IEnumerable<T> YieldAllContentBranches()
        {
            foreach (var item in this.Leaves)
            {
                if (item.Content != null)
                {
                    yield return item.Content;
                }

                var nextLevel = item.YieldAllContentBranches();

                foreach (var citem in nextLevel)
                {
                    yield return citem;
                }
            }
        }
    }

    public static class EBranch
    {
        public static Branch<T> ToBranch<T>(this IEnumerable<T> content)
        {
            var contentBranch = new Branch<T>();
            foreach (var item in content)
            {
                contentBranch.Leaves.Add(new Branch<T>() { Content = item });
            }

            return contentBranch;
        }
    }
}
