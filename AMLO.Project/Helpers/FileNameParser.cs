namespace AMLO.Project.Helpers
{
    /// <summary>
    /// Helper class to parse filename and extract TypeName and Version
    /// Expected filename format: {TypeName}_{Version}_{other_parts}.csv
    /// Example: SANCTION_20240115_20240120_1.csv -> TypeName: SANCTION, Version: 20240115
    /// </summary>
    public static class FileNameParser
    {
        private const char Delimiter = '_';

        /// <summary>
        /// Extracts TypeName from filename (index 0 after split by underscore)
        /// </summary>
        /// <param name="fileName">The filename to parse (e.g., "SANCTION_20240115_20240120_1.csv")</param>
        /// <returns>TypeName or empty string if parsing fails</returns>
        public static string ExtractTypeName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            var parts = Path.GetFileNameWithoutExtension(fileName).Split(Delimiter);
            return parts.Length > 0 ? parts[0] : string.Empty;
        }

        /// <summary>
        /// Extracts Version from filename (index 1 after split by underscore)
        /// </summary>
        /// <param name="fileName">The filename to parse (e.g., "SANCTION_20240115_20240120_1.csv")</param>
        /// <returns>Version or empty string if parsing fails</returns>
        public static string ExtractVersion(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            var parts = Path.GetFileNameWithoutExtension(fileName).Split(Delimiter);
            return parts.Length > 1 ? parts[1] : string.Empty;
        }

        /// <summary>
        /// Extracts both TypeName and Version from filename
        /// </summary>
        /// <param name="fileName">The filename to parse</param>
        /// <returns>Tuple of (TypeName, Version)</returns>
        public static (string TypeName, string Version) ParseFileName(string fileName)
        {
            return (ExtractTypeName(fileName), ExtractVersion(fileName));
        }

        /// <summary>
        /// Validates if filename has valid format
        /// </summary>
        public static bool IsValidFileName(string fileName)
        {
            var parts = Path.GetFileNameWithoutExtension(fileName).Split(Delimiter);
            return parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]);
        }
    }
}
