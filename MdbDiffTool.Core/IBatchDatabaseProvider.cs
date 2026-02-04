namespace MdbDiffTool.Core
{
    /// <summary>
    /// Опциональный интерфейс: позволяет провайдеру подготовить ресурс для серии операций
    /// (например, открыть и переиспользовать один и тот же коннект) и затем корректно его закрыть.
    ///
    /// Если провайдер не реализует интерфейс — всё работает как раньше.
    /// </summary>
    public interface IBatchDatabaseProvider
    {
        /// <summary>
        /// Начать пакетную операцию (ускорение серии вызовов к одной и той же БД).
        /// </summary>
        void BeginBatch(string connectionString);

        /// <summary>
        /// Завершить пакетную операцию и освободить ресурсы.
        /// </summary>
        void EndBatch(string connectionString);
    }
}
