using System;
using System.Configuration;

namespace ErikEJ.SqlCeMigrator
{
    class Program
    {
        static void Main(string[] args)
        {
            var localDbPath = ConfigurationManager.AppSettings["SqlCeDatabasePath"];

            var renameLocalDb = false;
            bool.TryParse(ConfigurationManager.AppSettings["RenameSqlCeDatabase"], out renameLocalDb);

            var scopeValue = -1;
            int.TryParse(ConfigurationManager.AppSettings["Scope"], out scopeValue);

            var ignoreTableString = ConfigurationManager.AppSettings["SqlCeTablesToIgnore"];
            string[] tablesToIgnore = new string[0];
            if (!string.IsNullOrWhiteSpace(ignoreTableString))
            {
                tablesToIgnore = ignoreTableString.Split(',');
            }

            var appendTableString = ConfigurationManager.AppSettings["SqlCeTablesToAppend"];
            string[] tablesToAppend = new string[0];
            if (!string.IsNullOrWhiteSpace(appendTableString))
            {
                tablesToAppend = appendTableString.Split(',');
            }

            var clearTableString = ConfigurationManager.AppSettings["SqlServerTablesToClear"];
            string[] tablesToClear = new string[0];
            if (!string.IsNullOrWhiteSpace(clearTableString))
            {
                tablesToClear = clearTableString.Split(',');
            }

            var targetConnectionString = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;

            var sqlCeMigrator = new SqlCeMigrator();

            try
            {
                if (!sqlCeMigrator.TryImport(localDbPath, tablesToIgnore, tablesToAppend, targetConnectionString, tablesToClear, renameLocalDb, scopeValue))
                {
                    Console.WriteLine("Migration failed, please review the SQL Compact database folder for block.txt");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
