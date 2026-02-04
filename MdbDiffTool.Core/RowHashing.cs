using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace MdbDiffTool.Core
{
    /// <summary>
    /// Утилиты для построения ключей и хешей строк.
    /// Хеширование сделано так, чтобы "равенство" соответствовало DiffEngine.RowsEqual
    /// (в частности: для string NULL == "").
    /// </summary>
    public static class RowHashing
    {
        // FNV-1a 64-bit
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static string BuildKey(IDataRecord record, int[] keyOrdinals)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (keyOrdinals == null) throw new ArgumentNullException(nameof(keyOrdinals));

            // По логике DiffEngine.BuildKey: NULL/DBNull -> "<NULL>", иначе ToString().
            var sb = new StringBuilder(64);
            for (int i = 0; i < keyOrdinals.Length; i++)
            {
                if (i > 0) sb.Append('|');

                int ord = keyOrdinals[i];

                if (record.IsDBNull(ord))
                {
                    sb.Append("<NULL>");
                }
                else
                {
                    object v = record.GetValue(ord);
                    if (v == null || v == DBNull.Value) sb.Append("<NULL>");
                    else sb.Append(v.ToString());
                }
            }

            return sb.ToString();
        }

        public static ulong ComputeRowHash(IDataRecord record, int[] ordinals, Type[] expectedTypes)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (ordinals == null) throw new ArgumentNullException(nameof(ordinals));
            if (expectedTypes == null) throw new ArgumentNullException(nameof(expectedTypes));
            if (ordinals.Length != expectedTypes.Length)
                throw new ArgumentException("ordinals.Length != expectedTypes.Length");

            ulong hash = FnvOffset;

            for (int i = 0; i < ordinals.Length; i++)
            {
                // Разделитель между столбцами, чтобы избежать "склеек"
                hash = AddByte(hash, 0x1F);

                int ord = ordinals[i];
                Type expected = expectedTypes[i] ?? typeof(object);

                object raw = record.IsDBNull(ord) ? null : record.GetValue(ord);
                object value = NormalizeValue(raw, expected);

                // Тип и значение
                hash = AddTypeMarker(hash, expected);

                if (expected == typeof(string))
                {
                    // По правилам DiffEngine.AreValuesEqual для string: NULL == ""
                    string s = value as string ?? "";
                    hash = AddString(hash, s);
                    continue;
                }

                if (value == null)
                {
                    // NULL для не-string типов должен отличаться от любого значения
                    hash = AddByte(hash, 0xFF);
                    continue;
                }

                // Важно: хешируем "канонично", независимо от культуры ОС.
                hash = AddValue(hash, value, expected);
            }

            return hash;
        }

        private static object NormalizeValue(object value, Type expectedType)
        {
            if (value == null || value == DBNull.Value)
            {
                // Для string: нормализуем в "" (NULL == "")
                if (expectedType == typeof(string)) return "";
                return null;
            }

            if (expectedType == typeof(string))
            {
                // В DataTable обычно string уже строка.
                if (value is string s) return s;
                return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
            }

            // Если уже того типа — оставляем
            if (expectedType.IsInstanceOfType(value))
                return value;

            // Особые типы
            if (expectedType == typeof(Guid))
            {
                if (value is Guid g) return g;
                if (value is string gs && Guid.TryParse(gs, out var parsed)) return parsed;
                return value;
            }

            if (expectedType == typeof(byte[]))
            {
                if (value is byte[] b) return b;
                return value;
            }

            try
            {
                // Convert.ChangeType хорошо приводит числа/DateTime/decimal и пр.
                return Convert.ChangeType(value, expectedType, CultureInfo.InvariantCulture);
            }
            catch
            {
                // Фолбэк — как есть (лучше, чем падать).
                return value;
            }
        }

        private static ulong AddTypeMarker(ulong hash, Type type)
        {
            // Лёгкая "метка типа", чтобы 1(int) не совпал с "1"(string) и т.п.
            // Для object/unknown используем 0.
            byte code = 0;
            if (type == typeof(string)) code = 1;
            else if (type == typeof(int)) code = 2;
            else if (type == typeof(long)) code = 3;
            else if (type == typeof(short)) code = 4;
            else if (type == typeof(byte)) code = 5;
            else if (type == typeof(bool)) code = 6;
            else if (type == typeof(double)) code = 7;
            else if (type == typeof(float)) code = 8;
            else if (type == typeof(decimal)) code = 9;
            else if (type == typeof(DateTime)) code = 10;
            else if (type == typeof(Guid)) code = 11;
            else if (type == typeof(byte[])) code = 12;
            else code = 0;

            return AddByte(hash, code);
        }

        private static ulong AddValue(ulong hash, object value, Type expected)
        {
            if (expected == typeof(int))
            {
                int v = (value is int i) ? i : Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return AddBytes(hash, BitConverter.GetBytes(v));
            }
            if (expected == typeof(long))
            {
                long v = (value is long l) ? l : Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return AddBytes(hash, BitConverter.GetBytes(v));
            }
            if (expected == typeof(short))
            {
                short v = (value is short s) ? s : Convert.ToInt16(value, CultureInfo.InvariantCulture);
                return AddBytes(hash, BitConverter.GetBytes(v));
            }
            if (expected == typeof(byte))
            {
                byte v = (value is byte b) ? b : Convert.ToByte(value, CultureInfo.InvariantCulture);
                return AddByte(hash, v);
            }
            if (expected == typeof(bool))
            {
                bool v = (value is bool bb) ? bb : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                return AddByte(hash, v ? (byte)1 : (byte)0);
            }
            if (expected == typeof(double))
            {
                double v = (value is double d) ? d : Convert.ToDouble(value, CultureInfo.InvariantCulture);
                long bits = BitConverter.DoubleToInt64Bits(v);
                return AddBytes(hash, BitConverter.GetBytes(bits));
            }
            if (expected == typeof(float))
            {
                float v = (value is float f) ? f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
                // В net48 нет SingleToInt32Bits, поэтому используем GetBytes напрямую.
                return AddBytes(hash, BitConverter.GetBytes(v));
            }
                        if (expected == typeof(decimal))
            {
                decimal v = (value is decimal dec) ? dec : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                int[] bits = decimal.GetBits(v);
                // 4x int
                hash = AddBytes(hash, BitConverter.GetBytes(bits[0]));
                hash = AddBytes(hash, BitConverter.GetBytes(bits[1]));
                hash = AddBytes(hash, BitConverter.GetBytes(bits[2]));
                hash = AddBytes(hash, BitConverter.GetBytes(bits[3]));
                return hash;
            }
            if (expected == typeof(DateTime))
            {
                DateTime dt = (value is DateTime dtt) ? dtt : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
                long bin = dt.ToBinary();
                return AddBytes(hash, BitConverter.GetBytes(bin));
            }
            if (expected == typeof(Guid))
            {
                Guid g = (value is Guid gg) ? gg : Guid.Parse(value.ToString());
                return AddBytes(hash, g.ToByteArray());
            }
            if (expected == typeof(byte[]))
            {
                var bytes = value as byte[] ?? Array.Empty<byte>();
                // длина + содержимое
                hash = AddBytes(hash, BitConverter.GetBytes(bytes.Length));
                return AddBytes(hash, bytes);
            }

            // Фолбэк: строковое представление в invariant (чтобы не зависеть от культуры)
            string s2 = value.ToString() ?? "";
            return AddString(hash, s2, invariant: true);
        }

        private static ulong AddString(ulong hash, string s, bool invariant = false)
        {
            if (s == null) s = "";
            // Для "фолбэка" используем invariant, для string-колонок — как есть (AreValuesEqual = ordinal).
            // Но в любом случае UTF8.
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            return AddBytes(hash, bytes);
        }

        private static ulong AddBytes(ulong hash, byte[] bytes)
        {
            if (bytes == null) return hash;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= FnvPrime;
            }
            return hash;
        }

        private static ulong AddByte(ulong hash, byte b)
        {
            hash ^= b;
            hash *= FnvPrime;
            return hash;
        }
    }
}
