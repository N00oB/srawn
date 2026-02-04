using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MdbDiffTool
{
    /// <summary>
    /// Сервис построения строк подключения.
    /// Вынесен из Form1, чтобы разгрузить UI-логику.
    /// </summary>
    internal sealed class ConnectionStringService
    {
        private static readonly Dictionary<string, string> PostgresUriQueryMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Наиболее частые параметры libpq URI
                { "sslmode", "SSL Mode" },
                { "ssl_mode", "SSL Mode" },
                { "trust_server_certificate", "Trust Server Certificate" },
                { "application_name", "Application Name" },
                { "options", "Options" },
                { "search_path", "Search Path" },
                { "searchpath", "Search Path" },

                // На всякий случай допускаем переопределения базовых полей
                { "host", "Host" },
                { "port", "Port" },
                { "database", "Database" },
                { "dbname", "Database" },
                { "user", "Username" },
                { "username", "Username" },
                { "password", "Password" },
            };

        /// <summary>
        /// Универсальная точка входа:
        /// - если input указывает на существующий файл, строка подключения строится по расширению;
        /// - иначе input трактуется как готовая строка подключения (например, PostgreSQL).
        /// </summary>
        public string BuildFromInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Путь к базе или строка подключения не задан(а).", nameof(input));

            input = input.Trim();

            if (File.Exists(input))
                return BuildForPath(input);

            // Поддержка PostgreSQL URI-формата:
            // postgresql://user:password@host:port/dbname?sslmode=require&search_path=myschema
            // Внутри приложения дальше ожидается обычная строка Npgsql вида Host=...;Port=...;...
            if (LooksLikePostgresUri(input))
                return ConvertPostgresUriToNpgsqlConnectionString(input);

            return input;
        }

        private static bool LooksLikePostgresUri(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            return input.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                   input.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Конвертирует PostgreSQL URI (postgresql://...) в обычный connection string Npgsql (Host=...;...)
        /// </summary>
        private static string ConvertPostgresUriToNpgsqlConnectionString(string uriText)
        {
            if (string.IsNullOrWhiteSpace(uriText))
                throw new ArgumentException("Строка подключения не задана.", nameof(uriText));

            // Некоторые провайдеры (например Heroku) используют postgres://
            if (uriText.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                uriText = "postgresql://" + uriText.Substring("postgres://".Length);

            if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
                throw new ArgumentException("Некорректный PostgreSQL URI.");

            if (!string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase))
            {
                // На всякий случай — если вдруг это не PostgreSQL URI
                return uriText;
            }

            // Сначала вытаскиваем базовые поля из URI
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(uri.Host))
                values["Host"] = uri.Host;

            // Если порт не указан, Uri.Port = -1 (Npgsql по умолчанию 5432)
            if (uri.Port > 0)
                values["Port"] = uri.Port.ToString();

            // /dbname (может быть пустым или просто "/")
            var db = (uri.AbsolutePath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(db) && db != "/")
                values["Database"] = Uri.UnescapeDataString(db.TrimStart('/'));

            // user[:password]
            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                var userInfo = uri.UserInfo;
                var parts = userInfo.Split(new[] { ':' }, 2);

                var user = parts.Length > 0 ? Uri.UnescapeDataString(parts[0]) : null;
                var pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;

                if (!string.IsNullOrWhiteSpace(user))
                    values["Username"] = user;

                if (!string.IsNullOrWhiteSpace(pass))
                    values["Password"] = pass;
            }

            // Теперь применяем query-параметры (если есть) — они могут дополнять/переопределять базовые поля
            ApplyPostgresUriQuery(values, uri.Query);

            // Собираем Npgsql connection string
            var sb = new StringBuilder();
            AppendIfExists(sb, values, "Host");
            AppendIfExists(sb, values, "Port");
            AppendIfExists(sb, values, "Database");
            AppendIfExists(sb, values, "Username");
            AppendIfExists(sb, values, "Password");

            // Остальные параметры (опционально)
            AppendIfExists(sb, values, "SSL Mode");
            AppendIfExists(sb, values, "Trust Server Certificate");
            AppendIfExists(sb, values, "Application Name");
            AppendIfExists(sb, values, "Search Path");
            AppendIfExists(sb, values, "Options");

            return sb.ToString();
        }

        private static void ApplyPostgresUriQuery(Dictionary<string, string> values, string query)
        {
            if (values == null)
                return;

            if (string.IsNullOrWhiteSpace(query))
                return;

            // query приходит в виде "?a=b&c=d"; аккуратно парсим без System.Web
            var q = query;
            if (q.StartsWith("?"))
                q = q.Substring(1);

            if (string.IsNullOrWhiteSpace(q))
                return;

            var pairs = q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in pairs)
            {
                var kv = p.Split(new[] { '=' }, 2);
                var rawKey = kv.Length > 0 ? kv[0] : null;
                if (string.IsNullOrWhiteSpace(rawKey))
                    continue;

                var rawVal = kv.Length > 1 ? kv[1] : string.Empty;

                var key = Uri.UnescapeDataString(rawKey);
                var val = Uri.UnescapeDataString(rawVal ?? string.Empty);

                if (PostgresUriQueryMap.TryGetValue(key, out var mappedKey))
                {
                    if (!string.IsNullOrWhiteSpace(val))
                        values[mappedKey] = val;
                }
            }
        }

        private static void AppendIfExists(StringBuilder sb, Dictionary<string, string> values, string key)
        {
            if (sb == null || values == null || string.IsNullOrWhiteSpace(key))
                return;

            if (!values.TryGetValue(key, out var value))
                return;

            if (string.IsNullOrWhiteSpace(value))
                return;

            sb.Append(key);
            sb.Append('=');
            sb.Append(EscapeConnectionStringValue(value));
            sb.Append(';');
        }

        private static string EscapeConnectionStringValue(string value)
        {
            if (value == null)
                return string.Empty;

            // По докам Npgsql значения с "особенными" символами (например ;) можно брать в двойные кавычки.
            // Чтобы не словить поломку парсинга, экранируем кавычки и при необходимости заключаем в кавычки.
            var needsQuotes = value.IndexOf(';') >= 0 ||
                              value.StartsWith(" ") ||
                              value.EndsWith(" ");

            if (!needsQuotes)
                return value;

            var escaped = value.Replace("\"", "\"\"");
            return "\"" + escaped + "\"";
        }

        /// <summary>
        /// Универсальный билдер строки подключения по расширению файла.
        /// </summary>
        public string BuildForPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к базе данных не задан.", nameof(path));

            string ext = Path.GetExtension(path)?.ToLowerInvariant();

            // SQLite
            if (string.Equals(ext, ".sqlite", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".db3", StringComparison.OrdinalIgnoreCase))
            {
                return BuildSqliteConnectionString(path);
            }

            if (string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".xlsm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".xlam", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".xls", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".xla", StringComparison.OrdinalIgnoreCase))
            {
                return $@"ExcelFile={path};";
            }

            // По умолчанию — старое поведение: считаем, что это Access
            return BuildAccessConnectionString(path);
        }

        /// <summary>
        /// Строит строку подключения к базе Access (.mdb или .accdb).
        /// Для .accdb используется ACE, для .mdb — Jet (только для x86) или ACE (для x64).
        /// </summary>
        public string BuildAccessConnectionString(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к базе данных не задан.", nameof(path));

            string ext = Path.GetExtension(path)?.ToLowerInvariant();
            bool isAccdb = string.Equals(ext, ".accdb", StringComparison.OrdinalIgnoreCase);

            // 1) Для .accdb нужен ACE.
            // 2) Для .mdb можно использовать Jet (только x86), либо ACE.
            // 3) На чужих ПК чаще стоит ACE 16.0 (Office 2016/365), поэтому сначала проверяем 16, затем 12.

            string provider = null;

            // Jet — только для .mdb и только в 32-битном процессе.
            if (!isAccdb && !Environment.Is64BitProcess && IsOleDbProviderInstalled("Microsoft.Jet.OLEDB.4.0"))
            {
                provider = "Microsoft.Jet.OLEDB.4.0";
            }
            else if (IsOleDbProviderInstalled("Microsoft.ACE.OLEDB.16.0"))
            {
                provider = "Microsoft.ACE.OLEDB.16.0";
            }
            else if (IsOleDbProviderInstalled("Microsoft.ACE.OLEDB.12.0"))
            {
                provider = "Microsoft.ACE.OLEDB.12.0";
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                // Делаем понятную ошибку до попытки Open(), чтобы она красиво показалась пользователю.
                if (isAccdb)
                {
                    throw new InvalidOperationException(
                        "Не найден драйвер Microsoft Access Database Engine (ACE OLEDB).\r\n" +
                        "Для работы с файлами .accdb необходимо установить Microsoft Access Database Engine\r\n" +
                        "той же разрядности, что и программа (x86 или x64).\r\n\r\n" +
                        "Если на ПК установлен Office x64, нужна версия программы x64;\r\n" +
                        "если установлен Office x86 — версия программы x86.");
                }

                throw new InvalidOperationException(
                    "Не найден OLEDB-провайдер для Access (.mdb).\r\n" +
                    "Установите Microsoft Access Database Engine (ACE) или используйте сборку x86 (Jet).");
            }

            return $"Provider={provider};Data Source={path};Persist Security Info=False;";
        }

        private static bool IsOleDbProviderInstalled(string progId)
        {
            if (string.IsNullOrWhiteSpace(progId))
                return false;

            try
            {
                // Для OLEDB провайдера достаточно регистрации ProgID.
                // Важно: проверка будет корректной для разрядности текущего процесса (x86/x64).
                return Type.GetTypeFromProgID(progId, throwOnError: false) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Строит строку подключения к базе SQLite.
        /// </summary>
        public string BuildSqliteConnectionString(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к базе данных не задан.", nameof(path));

            return $"Data Source={path};Version=3;";
        }
    }
}
