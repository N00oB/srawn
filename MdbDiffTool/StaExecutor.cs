using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MdbDiffTool
{
    /// <summary>
    /// Выполняет код в STA-потоках.
    ///
    /// Зачем:
    /// ACE/OLEDB (Access Database Engine) внутри использует COM/Office-компоненты.
    /// На части машин вызовы из ThreadPool (MTA) и/или агрессивная параллельность
    /// приводят к падению процесса (0xc0000005) в нативных DLL (mso.dll и т.п.).
    ///
    /// Решение:
    /// - Все операции Access выполняем только в STA.
    /// - Последовательно ВНУТРИ одной БД.
    /// - Но между двумя разными БД (source/target) разрешаем параллельность:
    ///   у нас есть 2 STA-воркера и мы закрепляем разные connectionString за разными воркерами.
    /// </summary>
    internal static class StaExecutor
    {
        private sealed class Worker
        {
            private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
            private readonly Thread _thread;
            private int _threadId;

            public Worker(string name)
            {
                _thread = new Thread(Loop)
                {
                    IsBackground = true,
                    Name = name
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }

            private void Loop()
            {
                _threadId = Thread.CurrentThread.ManagedThreadId;
                foreach (var action in _queue.GetConsumingEnumerable())
                {
                    try { action(); }
                    catch { /* исключения пробрасываются через TCS */ }
                }
            }

            public void Run(Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));

                if (Thread.CurrentThread.ManagedThreadId == _threadId)
                {
                    action();
                    return;
                }

                var tcs = new TaskCompletionSource<object>();
                _queue.Add(() =>
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                tcs.Task.GetAwaiter().GetResult();
            }

            public T Run<T>(Func<T> func)
            {
                if (func == null) throw new ArgumentNullException(nameof(func));

                if (Thread.CurrentThread.ManagedThreadId == _threadId)
                    return func();

                var tcs = new TaskCompletionSource<T>();
                _queue.Add(() =>
                {
                    try
                    {
                        var result = func();
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                return tcs.Task.GetAwaiter().GetResult();
            }
        }

        // Два STA-воркера: этого достаточно, чтобы грузить source и target параллельно,
        // но не устраивать "пул из 8" для ACE (что часто приводит к крэшам).
        private static readonly Worker[] _workers =
        {
            new Worker("MdbDiffTool.Access.STA-0"),
            new Worker("MdbDiffTool.Access.STA-1")
        };

        private static readonly object _gate = new object();
        private static readonly Dictionary<string, int> _keyToWorker = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _nextAssign;

        /// <summary>
        /// Старый режим (один воркер). Оставлено для обратной совместимости.
        /// </summary>
        public static void Run(Action action) => _workers[0].Run(action);

        /// <summary>
        /// Старый режим (один воркер). Оставлено для обратной совместимости.
        /// </summary>
        public static T Run<T>(Func<T> func) => _workers[0].Run(func);

        /// <summary>
        /// Выполнить действие в STA, закрепив все вызовы с одинаковым ключом за одним и тем же STA-воркером.
        /// Это гарантирует последовательность операций в рамках одной базы.
        /// </summary>
        public static void Run(string key, Action action)
        {
            GetWorker(key).Run(action);
        }

        /// <summary>
        /// Выполнить функцию в STA, закрепив ключ за конкретным STA-воркером.
        /// </summary>
        public static T Run<T>(string key, Func<T> func)
        {
            return GetWorker(key).Run(func);
        }

        private static Worker GetWorker(string key)
        {
            // Пустой ключ — используем воркер 0.
            if (string.IsNullOrWhiteSpace(key))
                return _workers[0];

            lock (_gate)
            {
                if (_keyToWorker.TryGetValue(key, out var idx))
                    return _workers[idx];

                idx = _nextAssign % _workers.Length;
                _nextAssign++;
                _keyToWorker[key] = idx;
                return _workers[idx];
            }
        }
    }
}
