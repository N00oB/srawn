using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Сервис для загрузки/сохранения конфигурации MdbDiffTool.config.xml.
    /// </summary>
    internal sealed class ConfigService
    {
        private readonly string _configPath;

        public ConfigService(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentNullException(nameof(configPath));

            _configPath = configPath;
        }

        /// <summary>
        /// Загружает конфигурацию из файла. При ошибке или отсутствии файла
        /// создаёт конфигурацию по умолчанию и пытается её сохранить.
        /// </summary>
        public AppConfig Load()
        {
            AppConfig config = null;

            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(_configPath))
                {
                    var ser = new XmlSerializer(typeof(AppConfig));
                    using (var fs = File.OpenRead(_configPath))
                    {
                        config = (AppConfig)ser.Deserialize(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(
                    "Ошибка при чтении файла конфигурации. " +
                    "Будет создана конфигурация по умолчанию.", ex);
            }

            if (config == null)
            {
                config = CreateDefaultConfig();
                try
                {
                    Save(config);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(
                        "Ошибка при создании файла конфигурации по умолчанию.", ex);
                }
            }

            EnsureCollections(config);
            EnsureDefaultExcluded(config);
            EnsureDefaults(config);

            // Обновляем файл конфига, чтобы новые поля появлялись сразу (удобно для ручного редактирования).
            try { Save(config); } catch { }

            return config;
        }

        /// <summary>
        /// Сохраняет конфигурацию в файл.
        /// </summary>
        public void Save(AppConfig config)
        {
            if (config == null)
            {
                config = CreateDefaultConfig();
            }

            EnsureCollections(config);
            EnsureDefaultExcluded(config);
            EnsureDefaults(config);

            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var ser = new XmlSerializer(typeof(AppConfig));
                using (var fs = File.Create(_configPath))
                {
                    ser.Serialize(fs, config);
                }
            }
            catch (Exception ex)
            {
                // Поведение как и раньше — ошибки конфигурации не должны валить приложение.
                AppLogger.Error("Ошибка при сохранении файла конфигурации.", ex);
            }
        }

        /// <summary>
        /// Возвращает множество исключённых таблиц (с Trim и без пустых строк).
        /// </summary>
        public HashSet<string> GetExcludedTablesSet(AppConfig config)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (config?.ExcludedTables != null)
            {
                foreach (var t in config.ExcludedTables)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                        set.Add(t.Trim());
                }
            }

            return set;
        }

        /// <summary>
        /// Конфигурация по умолчанию.
        /// </summary>
        private static AppConfig CreateDefaultConfig()
        {
            var cfg = new AppConfig
            {
                ExcludedTables = new List<string>(),
                CustomKeys = new List<CustomKeyConfig>()
            };

            // служебная таблица Access, не трогаем
            cfg.ExcludedTables.Add("Ошибки вставки");

            return cfg;
        }

        private static void EnsureCollections(AppConfig config)
        {
            if (config.ExcludedTables == null)
                config.ExcludedTables = new List<string>();

            if (config.CustomKeys == null)
                config.CustomKeys = new List<CustomKeyConfig>();
        }

        private static void EnsureDefaults(AppConfig config)
        {
            if (config == null)
                return;

            // На всякий случай, если конфиг старый/битый.
            if (config.MaxParallelTables <= 0)
                config.MaxParallelTables = 4;

            if (config.LastSourceBrowseFilterIndex <= 0)
                config.LastSourceBrowseFilterIndex = 1;

            if (config.LastTargetBrowseFilterIndex <= 0)
                config.LastTargetBrowseFilterIndex = 1;
        }

        /// <summary>
        /// Гарантирует наличие служебной таблицы "Ошибки вставки" в списке исключений.
        /// </summary>
        private static void EnsureDefaultExcluded(AppConfig config)
        {
            if (config.ExcludedTables == null)
                return;

            bool hasStandard = false;
            foreach (var t in config.ExcludedTables)
            {
                if (string.Equals(t, "Ошибки вставки", StringComparison.OrdinalIgnoreCase))
                {
                    hasStandard = true;
                    break;
                }
            }

            if (!hasStandard)
                config.ExcludedTables.Add("Ошибки вставки");
        }
    }
}
