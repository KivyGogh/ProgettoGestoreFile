﻿using System;
using System.Data;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Resources;

namespace clientWPF
{
    class DB_Table
    {
        private SQLiteCommand command;
        private SQLiteDataReader reader;
        //private SQLiteDataAdapter DB;
        private DataSet DS = new DataSet();
        private DataTable DT = new DataTable();

        //static private bool flag;
        static String nome_file_db = Properties.Settings.Default.nome_db;
        static private int count_ref = 0;
        static private SQLiteConnection sql_con = null;

        private Log l;
        static private object lockVar = new object();
        static private bool locked = false;
        static private string[] db_structure = {
                        "drop table if exists versioni",
                        "drop table if exists file;",
                        //Tabella fileutente

                        Properties.SQLquery.tabellaFile,
                        Properties.SQLquery.TabellaVersioni,
                                                     };

        public DB_Table()
        {
            l = Log.getLog();
            lock (lockVar)
            {
                if (sql_con == null)
                {
                    count_ref = 0;
                    String s = "Data Source=";
                    s += nome_file_db + ";Versione=3;";
                    if (!File.Exists(nome_file_db))
                    {
                        l.log("DB non esistente. Devo crearlo");
                        SQLiteConnection.CreateFile(nome_file_db);
                        DB_Table.sql_con = new SQLiteConnection(s);
                        DB_Table.sql_con.Open();
                        Crea_DB();
                        l.log("DB creato");
                    }
                    else
                    {
                        DB_Table.sql_con = new SQLiteConnection(s);
                        DB_Table.sql_con.Open();
                    }
                }
                count_ref++;
                this.reader = null;
            }

        }


        static public bool DBEsiste => File.Exists(nome_file_db);
        static public string HashDB => FileUtente.CalcolaSHA256(File.OpenRead(nome_file_db));

        static public bool LockDB()
        {
            lock (lockVar)
            {
                if (locked == true)
                {
                    return false;
                }
                else
                {
                    locked = true;
                    return true;
                }
            }

        }


        ~DB_Table()
        {
            lock(lockVar)
            {
                count_ref--;
                if (count_ref == 0)
                {
                    try
                    {
                        DB_Table.sql_con.Close();
                    }
                    catch (ObjectDisposedException e)
                    {
                        l.log("Warning! La connessione è già stata chiusa ma non so come mai -.-. " + e.Message);
                    }
                    DB_Table.sql_con = null;
                }
            }
        }

        static public void RebuildDB()
        {
            String s = "Data Source=";
            s += nome_file_db + ";Versione=3;";
            lock (lockVar)
            {
                if (DB_Table.sql_con != null)
                {
                    DB_Table.sql_con.Close();
                    DB_Table.sql_con.Dispose();
                }
                /*
                if (File.Exists(nome_file_db))
                {
                    File.Delete(nome_file_db);
                }
                SQLiteConnection.CreateFile(nome_file_db);
                */
                DB_Table.sql_con = new SQLiteConnection(s);
                DB_Table.sql_con.Open();
                Crea_DB();
            }
        }

        static private void Crea_DB()
        {
            SQLiteCommand command;
            lock(lockVar)
            {
                command = sql_con.CreateCommand();
                foreach (string sql in db_structure)
                {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }

                //Alcuni valori di test:
                Properties.DatiTest.Culture = CultureInfo.CurrentCulture;
                ResourceSet rs = Properties.DatiTest.ResourceManager
                                            .GetResourceSet(CultureInfo.CurrentCulture, true, true);

                foreach (DictionaryEntry sql in rs)
                {
                    command.CommandText = sql.Value.ToString();
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Esegue una query sql parametrizzata.
        /// Errori NON GESTITI!!
        /// </summary>
        /// <param name="txtQuery">Query sql con o senza parametri</param>
        /// <param name="parameters">
        ///     Parametri da sostituire; 
        ///     Formato: parameters[i] = {"@nome_parametro","valore da sostituire"}
        /// </param>
        public void ExecuteQuery(string txtQuery, string[][] parameters = null)
        {
            lock(lockVar)
            {
                command = sql_con.CreateCommand();
            }
            command.CommandText = txtQuery;
            if (parameters != null)
            {
                foreach (string[] param in parameters)
                {
                    command.Parameters.Add(new SQLiteParameter(param[0], param[1]));
                }
            }
            try
            {
                command.VerifyOnly();
            }
            catch (Exception e)
            {
                l.log("Query errata: " + e.Message);
            }
            try
            {
                reader = command.ExecuteReader();
            }
            catch (SQLiteException e) when (e.ErrorCode == (int)SQLiteErrorCode.Constraint)
            {
                throw new DatabaseException("Errore di Constraint", DatabaseErrorCode.Constraint);
            }
        }

        /// <summary>
        /// Ritorna un iteratore per leggere i risultati di un query
        /// uno alla volta con il costrutto "foreach"
        /// </summary>
        /// <returns>
        /// Iteratore su stringa (MODIFICARE!!!)
        /// </returns>
        public IEnumerable<int> GetResults()
        {
            int i = 0;
            if (this.reader != null)
            {
                try
                {
                    while (reader.Read())
                    {
                        yield return ++i;
                    }
                }
                finally
                {
                    reader.Close();
                    reader = null;
                }
            }
        }

        public object ResultGetValue(string field_name)
        {
            return reader[field_name];
        }

        public long getLastInsertedId()
        {
            lock (lockVar)
            {
                command = sql_con.CreateCommand();
            }
            string sql = "select last_insert_rowid()";
            command.CommandText = sql;
            return (long)command.ExecuteScalar();
        }

        public void BeginTransaction()
        {
            string sql = "BEGIN TRANSACTION;";
            this.ExecuteQuery(sql);
        }
        public void CommitTransaction()
        {
            string sql = "COMMIT TRANSACTION;";
            this.ExecuteQuery(sql);
        }
        public void RollbackTransaction()
        {
            string sql = "ROLLBACK TRANSACTION;";
            this.ExecuteQuery(sql);
        }
    }
}