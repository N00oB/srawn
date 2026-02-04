using System;

namespace MdbDiffTool.Core
{
    /// <summary>
    /// Описание столбца таблицы (минимально необходимое для быстрого хеширования строк).
    /// </summary>
    public sealed class ColumnInfo
    {
        public string Name { get; }
        public Type DataType { get; }

        public ColumnInfo(string name, Type dataType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DataType = dataType ?? typeof(object);
        }
    }
}
