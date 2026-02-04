using System;
using System.Collections.Generic;

namespace MdbDiffTool.Core
{
    [Serializable]
    public sealed class AppConfig
    {
        public string LastSourcePath { get; set; }
        public string LastTargetPath { get; set; }

        public List<string> ExcludedTables { get; set; } = new List<string>();
        public List<CustomKeyConfig> CustomKeys { get; set; } = new List<CustomKeyConfig>();
        public int MaxParallelTables { get; set; } = 4;
    }

    [Serializable]
    public sealed class CustomKeyConfig
    {
        public string TableName { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
    }
}
