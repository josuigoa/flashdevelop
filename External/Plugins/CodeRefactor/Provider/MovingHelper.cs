﻿using CodeRefactor.Commands;
using PluginCore;
using PluginCore.FRService;
using PluginCore.Managers;
using System;
using System.Collections.Generic;

namespace CodeRefactor.Provider
{
    class MovingHelper
    {
        private static List<QueueItem> queue = new List<QueueItem>();
        private static Dictionary<string, List<SearchMatch>> results = new Dictionary<string, List<SearchMatch>>();
        private static Move currentCommand;

        public static void AddToQueue(Dictionary<string, string> oldPathToNewPath)
        {
            AddToQueue(oldPathToNewPath, false);
        }

        public static void AddToQueue(Dictionary<string, string> oldPathToNewPath, bool outputResults)
        {
            AddToQueue(oldPathToNewPath, outputResults, false);
        }

        public static void AddToQueue(Dictionary<string, string> oldPathToNewPath, bool outputResults, bool renaming)
        {
            queue.Add(new QueueItem(oldPathToNewPath, outputResults, renaming));
            if (currentCommand == null) MoveFirst();
        }

        private static void MoveFirst()
        {
            try
            {
                QueueItem item = queue[0];
                Dictionary<string, string> oldPathToNewPath = item.oldPathToNewPath;
                queue.Remove(item);
                currentCommand = new Move(oldPathToNewPath, item.outputResults, item.renaming);
                currentCommand.OnRefactorComplete += OnRefactorComplete;
                currentCommand.Execute();
            }
            catch(Exception ex)
            {
                queue.Clear();
                results.Clear();
                currentCommand = null;
                ErrorManager.ShowError(ex);
            }
        }

        private static void OnRefactorComplete(object sender, RefactorCompleteEventArgs<IDictionary<string, List<SearchMatch>>> e)
        {
            if (currentCommand.OutputResults)
            {
                foreach (KeyValuePair<string, List<SearchMatch>> entry in currentCommand.Results)
                {
                    string path = entry.Key;
                    if (!results.ContainsKey(path)) results[path] = new List<SearchMatch>();
                    results[path].AddRange(entry.Value);
                }
            }
            if (queue.Count > 0) MoveFirst();
            else
            {
                if (results.Count > 0) ReportResults();
                results.Clear();
                currentCommand = null;
            }
        }

        private static void ReportResults()
        {
            PluginBase.MainForm.CallCommand("PluginCommand", "ResultsPanel.ClearResults");
            foreach (KeyValuePair<string, List<SearchMatch>> entry in results)
            {
                Dictionary<int, int> lineOffsets = new Dictionary<int, int>();
                Dictionary<int, string> lineChanges = new Dictionary<int, string>();
                Dictionary<int, List<string>> reportableLines = new Dictionary<int, List<string>>();
                foreach (SearchMatch match in entry.Value)
                {
                    int column = match.Column;
                    int lineNumber = match.Line;
                    string changedLine = lineChanges.ContainsKey(lineNumber) ? lineChanges[lineNumber] : match.LineText;
                    int offset = lineOffsets.ContainsKey(lineNumber) ? lineOffsets[lineNumber] : 0;
                    column = column + offset;
                    lineChanges[lineNumber] = changedLine;
                    lineOffsets[lineNumber] = offset + (match.Value.Length - match.Length);
                    if (!reportableLines.ContainsKey(lineNumber)) reportableLines[lineNumber] = new List<string>();
                    reportableLines[lineNumber].Add(entry.Key + ":" + match.Line + ": chars " + column + "-" + (column + match.Value.Length) + " : {0}");
                }
                foreach (KeyValuePair<int, List<string>> lineSetsToReport in reportableLines)
                {
                    string renamedLine = lineChanges[lineSetsToReport.Key].Trim();
                    foreach (string lineToReport in lineSetsToReport.Value)
                    {
                        PluginCore.Managers.TraceManager.Add(string.Format(lineToReport, renamedLine), (int)TraceType.Info);
                    }
                }
            }
            PluginBase.MainForm.CallCommand("PluginCommand", "ResultsPanel.ShowResults");
        }
    }

    #region Helpers

    internal class QueueItem
    {
        public Dictionary<string, string> oldPathToNewPath;
        public bool outputResults;
        public bool renaming;

        public QueueItem(Dictionary<string, string> oldPathToNewPath, bool outputResults, bool renaming)
        {
            this.oldPathToNewPath = oldPathToNewPath;
            this.outputResults = outputResults;
            this.renaming = renaming;
        }
    }

    #endregion
}