using System;
using System.Collections.Generic;
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

            var removeTempFiles = false;
            bool.TryParse(ConfigurationManager.AppSettings["RemoveTempScripts"], out removeTempFiles);

            var scopeValue = -1;
            int.TryParse(ConfigurationManager.AppSettings["Scope"], out scopeValue);

            var ignoreTableString = ConfigurationManager.AppSettings["SqlCeTablesToIgnore"];
            var tablesToIgnore = new List<string>();
            if (!string.IsNullOrWhiteSpace(ignoreTableString))
            {
                tablesToIgnore.AddRange(ignoreTableString.Split(','));
            }

            var appendTableString = ConfigurationManager.AppSettings["SqlCeTablesToAppend"];
            var tablesToAppend = new List<string>();
            if (!string.IsNullOrWhiteSpace(appendTableString))
            {
                tablesToAppend.AddRange(appendTableString.Split(','));
            }

            var clearTableString = ConfigurationManager.AppSettings["SqlServerTablesToClear"];
            var tablesToClear = new List<string>();
            if (!string.IsNullOrWhiteSpace(clearTableString))
            {
                tablesToClear.AddRange(clearTableString.Split(','));
            }

            var targetConnectionString = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;

            var sqlCeMigrator = new SqlCeMigrator();

            try
            {
                if (!sqlCeMigrator.TryImport(localDbPath, tablesToIgnore, tablesToAppend, targetConnectionString, tablesToClear, renameLocalDb, removeTempFiles, scopeValue))
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
