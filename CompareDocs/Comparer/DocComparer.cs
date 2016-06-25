﻿using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CompareDocs.Extensions;
using Novacode;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CompareDocs.Comparer
{
    public class DocComparer
    {
        private readonly string _sourceFilePath;
        private readonly string _targetFilePath;
        private int _exists;
        private int _total;

        public DocComparer(string source, string target)
        {
            _sourceFilePath = source;
            _targetFilePath = target;
        }

        public string ElapsedTime { get; private set; }
        public string ComparedFile { get; private set; }

        public float Compare()
        {
            var filePath = Helpers.GetTempFile(_sourceFilePath);
            var stopWatch = new Stopwatch();
            var formatting = new Formatting
            {
                Bold = true,
                FontColor = Color.Red
            };

            stopWatch.Start();

            File.Copy(_sourceFilePath, filePath, true);

            var totalSourceChunks = new List<string>();
            using (var sourceDoc = DocX.Load(filePath))
            {
                var sourceParagraphs = sourceDoc.Paragraphs.Where(w => !string.IsNullOrWhiteSpace(w.Text));

                foreach (var sourceParagraph in sourceParagraphs)
                {
                    var sourcePhrases = sourceParagraph.Text.ToSplit(".");
                    var sourceChunks = sourcePhrases.SelectMany(s => s.ToSplit(Helpers.Options.EXCLUDE_CHARACTERS));

                    totalSourceChunks.AddRange(sourceChunks);
                }

            }

            var totalTargetChunks = new List<string>();
            using (var targetDoc = DocX.Load(_targetFilePath))
            {
                var targetParagraphs = targetDoc.Paragraphs.Where(w => !string.IsNullOrWhiteSpace(w.Text));

                _total = targetParagraphs.Sum(s => s.Text.Length);

                foreach (var targetParagraph in targetParagraphs)
                {
                    var targetPhrases = targetParagraph.Text.ToSplit(".");
                    var targetChunks = targetPhrases.SelectMany(s => s.ToSplit(Helpers.Options.EXCLUDE_CHARACTERS));

                    totalTargetChunks.AddRange(targetChunks);
                }
            }

            using (var sourceDoc = DocX.Load(filePath))
            {
                var exists = totalTargetChunks.FindAll(f => totalSourceChunks.Exists(e => string.CompareOrdinal(f, e) == 0));

                Partitioner.Create(0, exists.Count, Environment.ProcessorCount)
                    .AsParallel()
                    .WithDegreeOfParallelism(Environment.ProcessorCount)
                    .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                    .WithMergeOptions(ParallelMergeOptions.FullyBuffered)
                    .ForAll(tuple =>
                    {
                        for (var index = tuple.Item1; index < tuple.Item2; index++)
                        {
                            try
                            {
                                sourceDoc.ReplaceText
                                (
                                    exists[index],
                                    exists[index],
                                    false,
                                    RegexOptions.IgnoreCase,
                                    formatting,
                                    null,
                                    MatchFormattingOptions.ExactMatch
                                );
                            }
                            catch (Exception)
                            {
                            }

                            _exists += exists[index].Length;
                        }
                    });

                sourceDoc.Save();
            }

            stopWatch.Stop();

            ElapsedTime = string.Format("{0}:{1}:{2}", stopWatch.Elapsed.Hours.ToString("00"),
                stopWatch.Elapsed.Minutes.ToString("00"), stopWatch.Elapsed.Seconds.ToString("00"));
            ComparedFile = filePath;

            return _exists * 1F / _total;
        }
    }
}