using System;
using System.Data;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// This class contains a set of extension methods for the <see cref="IDataReader"/> interface.
    /// </summary>
    public static class DataReaderExtensions
    {
        /// <summary>
        /// Gets a value from the current record of the given data reader, or the default value 
        /// for the given type <typeparamref name="T"/> if the reader value is <see cref="DBNull.Value"/>.
        /// </summary>
        /// <typeparam name="T">Type of value to retrieve.</typeparam>
        /// <param name="reader">Data reader positioned at a row.</param>
        /// <param name="colName">The column to get data from.</param>
        /// <returns>The reader value when present, otherwise the default value.</returns>
        public static T GetValue<T>(this IDataReader reader, string colName)
        {
            return GetValue<T>(reader, colName, default);
        }

        /// <summary>
        /// Gets a value from the current record of the given data reader, or the given default value 
        /// if the reader value is <see cref="DBNull.Value"/>.
        /// </summary>
        /// <typeparam name="T">Type of value to retrieve.</typeparam>
        /// <param name="reader">Data reader positioned at a row.</param>
        /// <param name="colName">The column to get data from.</param>
        /// <param name="defaultValue">Default value to use if the reader value is <see cref="DBNull.Value"/>.</param>
        /// <returns>The reader value when present, otherwise the given default value.</returns>
        public static T GetValue<T>(this IDataReader reader, string colName, T defaultValue)
        {
            object databaseValue = reader[colName];
            try
            {
                if (databaseValue is T value)
                {
                    return value;
                }

                if (databaseValue == DBNull.Value)
                {
                    return defaultValue;
                }

                if (typeof(T).IsEnum)
                {
                    if (databaseValue is int)
                    {
                        return (T)databaseValue;
                    }
                    else
                    {
                        return (T)Enum.Parse(typeof(T), databaseValue.ToString());
                    }
                }

                if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return (T)databaseValue;
                }

                return (T)Convert.ChangeType(databaseValue, typeof(T));
            }
            catch (Exception ex)
            {
                const string Message
                    = "Error trying to interpret data in column '{0}'. The reader value is '{1}', of type '{2}'. "
                    + "Attempt to interpret the value as type '{3}' failed.";

                string strVal = databaseValue.ToString();
                if (strVal.Length > 100)
                {
                    strVal = string.Format($"{strVal.Substring(0, 100)} (truncated; length={strVal.Length}).");
                }

                throw new InvalidCastException(string.Format(Message, colName, strVal, databaseValue.GetType(), typeof(T)), ex);
            }
        }
    }
}
