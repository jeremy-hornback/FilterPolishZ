﻿using FilterCore;
using FilterCore.Entry;
using FilterCore.Line;
using FilterExo.Core.PreProcess.Commands;
using FilterExo.Core.Structure;
using FilterPolishUtil;
using FilterPolishUtil.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static FilterExo.FilterExoConfig;
using static FilterPolishUtil.TraceUtility;

namespace FilterExo.Model
{
    public class ExoBlock
    {
        public ExoFilterType Type;
        public string Name;

        // Hierarchical elements
        public ExoBlock Parent;
        public List<ExoBlock> Scopes = new List<ExoBlock>();

        public Dictionary<string, ExoAtom> Variables { get; set; } = new Dictionary<string, ExoAtom>();
        public Dictionary<string, ExoAtom> Functions { get; set; } = new Dictionary<string, ExoAtom>();
        public List<ExoExpressionCommand> Commands { get; set; } = new List<ExoExpressionCommand>();

        public IEnumerable<List<string>> ResolveAndSerialize()
        {
            for (int i = 0; i < Commands.Count; i++)
            {
                ExoExpressionCommand comm = this.Commands[i];
                var result = comm.Serialize();
                if (result.Count == 0)
                {
                    continue;
                }

                yield return result;
            }
        }

        public void StoreVariable(string name, List<string> variableContent)
        {
            Check(FilterCore.FilterGenerationConfig.TierTagSort.ContainsKey(name), "variable uses reserved name!");
            Check(FilterCore.FilterGenerationConfig.LineTypesSort.ContainsKey(name), "variable uses reserved name!");
            Check(FilterCore.FilterGenerationConfig.ValidRarities.Contains(name), "variable uses reserved name!");
            Check(name.ContainsSpecialCharacters(), "variable uses invalid characters!");

            // we treat the variable as a command/expression to allow internal simplifications
            var command = new ExoExpressionCommand(variableContent);
            command.SetParent(this);
            var content = command.ResolveExpression();

            // since we don't know if there's 1 or many variables after the simplifications, we store result as a "pack"
            this.Variables.Add(name, new ExoAtom(content));
        }

        public ExoAtom GetVariable(string key)
        {
            return GetInternalVariable(key);
        }

        public ExoAtom GetFunction(string key)
        {
            return GetInternalFunction(key);
        }

        internal bool IsFunction(string key)
        {
            if (this.Functions.ContainsKey(key))
            {
                return true;
            }

            if (this.Type == ExoFilterType.root)
            {
                return false;
            }

            return this.GetParent().IsFunction(key);
        }

        public bool IsVariable(string key)
        {
            if (this.Variables.ContainsKey(key))
            {
                return true;
            }

            if (this.Type == ExoFilterType.root)
            {
                return false;
            }

            return this.GetParent().IsVariable(key);
        }

        private ExoAtom GetInternalVariable(string key)
        {
            if (this.Variables.ContainsKey(key))
            {
                return this.Variables[key];
            }

            return this.GetParent().GetVariable(key);
        }

        private ExoAtom GetInternalFunction(string key)
        {
            if (this.Functions.ContainsKey(key))
            {
                return this.Functions[key];
            }

            return this.GetParent().GetFunction(key);
        }

        public ExoBlock GetParent()
        {
            if (this.Type == ExoFilterType.root)
            {
                LoggingFacade.LogWarning("Attempting to get parent of root!");
                throw new Exception("Attempting to get parent of root!");
            }

            return this.Parent;
        }

        public void AddCommand(ExoExpressionCommand command)
        {
            this.Commands.Add(command);
            command.SetParent(this);
        }

        public List<string> Debug_GetSummary()
        {
            var results = new List<string>();
            results.Add($"TYPE: {this.Type.ToString()} // CHILDREN: {this.Scopes.Count}");
            results.Add($"VARIABLES: {string.Join(" ", this.Variables.Keys)}");
            results.AddRange(this.Commands.Select(x => x.SerializeDebug()));
            return results;
        }
    }
}
