using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

using ErikEJ.SqlCeScripting;

namespace ErikEJ.SqlCeMigrator
{
    public class SqlCeMigrator
    {
        public bool TryImport(string localDbPath, string[] tablesToIgnore, string[] tablesToAppend, string targetConnectionString, string[] tablesToClear, bool renameSource, bool removeTempFiles, int scopeValue)
        {
            if (string.IsNullOrEmpty(localDbPath))
            {
                throw new ArgumentNullException(nameof(localDbPath));
            }

            if (string.IsNullOrEmpty(targetConnectionString))
            {
                throw new ArgumentNullException(nameof(targetConnectionString));
            }

            if (!File.Exists(localDbPath))
            {
                Console.WriteLine("File not found: " + localDbPath);
                return false;
            }

            if (File.Exists(GetBlockFileName(localDbPath)))
            {
                Console.WriteLine("Block.txt found, will stop migration");
                return false;
            }

            var scope = GetScope(scopeValue);

            if (!ValidateTargetConnection(targetConnectionString, localDbPath))
            {
                return false;
            }

            ClearTargetTables(targetConnectionString, tablesToClear, localDbPath);

            // Ignore the tables that should be ignored OR that we will be appending to later
            string[] combined = new string[tablesToIgnore.Length + tablesToAppend.Length];
            Array.Copy(tablesToIgnore, combined, tablesToIgnore.Length);
            Array.Copy(tablesToAppend, 0, combined, tablesToIgnore.Length, tablesToAppend.Length);
            RunMigration(localDbPath, combined, targetConnectionString, scope, removeTempFiles);
            
            if (scope == Scope.DataOnlyForSqlServer && tablesToAppend.Length > 0)
            {
                RunMigration(localDbPath, tablesToAppend, targetConnectionString, Scope.DataOnlyForSqlServerIgnoreIdentity, removeTempFiles);
            }

            if (renameSource) RenameLocalDb(localDbPath);
            return true;
        }

        private void RunMigration(string localDbPath, string[] tablesToIgnoreOrAppend, string targetConnectionString, Scope scope, bool removeTempFiles)
        {
            using (var repository = new DB4Repository($"Data Source={localDbPath};Max Database Size=4000"))
            {
                var scriptRoot = Path.GetTempFileName();
                var tempScript = scriptRoot + ".sqltb";
                var generator = new Generator4(repository, tempScript);

                if (scope == Scope.DataOnlyForSqlServerIgnoreIdentity)
                {
                    //Ignore all tables except the ones in tablesToAppend
                    var tables = repository.GetAllTableNames();
                    var list = tables.Except(tablesToIgnoreOrAppend.ToList());
                    generator.ExcludeTables(list.ToList());
                }
                else
                {
                    generator.ExcludeTables(tablesToIgnoreOrAppend.ToList());
                }

                generator.ScriptDatabaseToFile(scope);
                using (var serverRepository = new ServerDBRepository4(targetConnectionString))
                {
                    try
                    {
                        //Handles large exports also... 
                        if (File.Exists(tempScript)) // Single file
                        {
                            serverRepository.ExecuteSqlFile(tempScript);
                            if (removeTempFiles) TryDeleteFile(tempScript);
                        }
                        else // possibly multiple files - tmp2BB9.tmp_0.sqlce
                        {
                            for (var i = 0; i < 400; i++)
                            {
                                var testFile = string.Format("{0}_{1}{2}", scriptRoot, i.ToString("D4"), ".sqltb");
                                if (File.Exists(testFile))
                                {
                                    serverRepository.ExecuteSqlFile(testFile);
                                    if (removeTempFiles) TryDeleteFile(testFile);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CreateBlockFile(localDbPath, ex);
                        throw;
                    }
                }
            }
        }

        private Scope GetScope(int scopeValue)
        {
            var scope = Scope.Schema;
            switch (scopeValue)
            {
                case 0:
                    scope = Scope.DataOnlyForSqlServer;
                    break;
                case 1:
                    scope = Scope.Schema;
                    break;
                case 2:
                    scope = Scope.SchemaData;
                    break;

                default:
                    throw new Exception("Invalid scope value");
            }
            return scope;
        }

        private bool ValidateTargetConnection(string targetConnectionString, string localDbPath)
        {
            try
            {
                using (var connection = new SqlConnection(targetConnectionString))
                {
                    using (var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables", connection))
                    {
                        connection.Open();
                        var result = (int)command.ExecuteScalar();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                CreateBlockFile(localDbPath, ex);
                throw;
            }
        }

        private void ClearTargetTables(string targetConnectionString, string[] tablesToClear, string localDbPath)
        {
            try
            {
                using (var connection = new SqlConnection(targetConnectionString))
                {
                    connection.Open();

                    foreach (var table in tablesToClear)
                    {
                        using (var command = new SqlCommand(string.Format("DELETE FROM {0}", table), connection))
                        {
                            var result = command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CreateBlockFile(localDbPath, ex);
                throw;
            }
        }

        private void RenameLocalDb(string localDbPath)
        {
            if (File.Exists(localDbPath))
            {
                var now = DateTime.Now;
                var newName = Path.Combine(Path.GetDirectoryName(localDbPath),
                    $"Accuvax-{now.Year}-{now.Month}-{now.Day}-{now.Hour}-{now.Minute}-{now.Second}.sdf");
                File.Move(localDbPath, newName);
            }
        }

        private void CreateBlockFile(string localDbPath, Exception ex)
        {
            if (File.Exists(localDbPath))
            {
                var blockText = ex.ToString();
                File.WriteAllText(GetBlockFileName(localDbPath), blockText, Encoding.UTF8);
            }
        }

        private string GetBlockFileName(string localDbPath)
        {
            return Path.Combine(Path.GetDirectoryName(localDbPath), "block.txt");
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                //Ignored
            }
        }
    }
}