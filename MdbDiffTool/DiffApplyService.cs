using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Сервис для применения diff-изменений к базе по данным из грида.
    /// </summary>
    internal sealed class DiffApplyService
    {
        private readonly IDatabaseProvider _dbProvider;

        public DiffApplyService(IDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        /// <summary>
        /// Собирает из грида список RowPair, которые реально нужно применять.
        /// Учитывает колонку "Apply" и тип diff (OnlyInSource / Different).
        /// </summary>
        public List<RowPair> CollectPairsToApply(DataGridView grid)
        {
            var result = new List<RowPair>();
            if (grid == null)
                return result;

            // На всякий случай убеждаемся, что у грида вообще есть колонка Apply
            var applyColumn = grid.Columns["Apply"];
            if (applyColumn == null)
                return result;

            foreach (DataGridViewRow gridRow in grid.Rows)
            {
                if (gridRow.IsNewRow)
                    continue;

                 if (!gridRow.Visible)
                     continue;

                var applyCell = gridRow.Cells["Apply"];
                if (applyCell == null || applyCell.ReadOnly)
                    continue;

                bool apply = applyCell.Value is bool b && b;
                if (!apply)
                    continue;

                if (gridRow.Tag is not RowPair pair)
                    continue;

                result.Add(pair);
            }

            return result;
        }


        /// <summary>
        /// Асинхронно применяет изменения к базе-приёмнику для заданных строк.
        /// </summary>
        public Task ApplyAsync(
            string targetConnectionString,
            DiffContext context,
            IEnumerable<RowPair> pairsToApply,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetConnectionString))
                throw new ArgumentException("Пустая строка подключения к базе-приёмнику.", nameof(targetConnectionString));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (pairsToApply == null)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                _dbProvider.ApplyRowChanges(
                    targetConnectionString,
                    context.TableName,
                    context.PrimaryKeyColumns,
                    pairsToApply);
            }, cancellationToken);
        }
    }
}
