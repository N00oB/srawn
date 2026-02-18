using System;
using System.IO;
using System.Xml.Serialization;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Минимальная загрузка конфигурации на этапе старта.
    /// Нужна, чтобы настроить путь логов ДО первого сообщения в лог.
    /// </summary>
    internal static class ConfigBootstrap
    {
        public static AppConfig TryLoad(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                return null;

            try
            {
                if (!File.Exists(configPath))
                    return null;

                var ser = new XmlSerializer(typeof(AppConfig));
                using (var fs = File.OpenRead(configPath))
                {
                    return (AppConfig)ser.Deserialize(fs);
                }
            }
            catch
            {
                // На старте намеренно молчим: логгер ещё не настроен.
                return null;
            }
        }
    }
}
