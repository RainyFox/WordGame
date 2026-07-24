using UnityEngine;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

public class SimpleDB
{
    private string connectString;
    public SimpleDB(string path)
    {
        connectString = "Data Source ="+path;
        EnsureUserProgressNextReviewColumn();
    }

    private void EnsureUserProgressNextReviewColumn()
    {
        using (var connection = new SqliteConnection(connectString))
        {
            connection.Open();

            bool tableExists = false;
            bool columnExists = false;
            using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.CommandText = "PRAGMA table_info(UserProgress)";
                using (var reader = checkCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tableExists = true;
                        if (string.Equals(reader.GetString(1), "NextReview", StringComparison.OrdinalIgnoreCase))
                        {
                            columnExists = true;
                            break;
                        }
                    }
                }
            }

            if (!tableExists)
                return;

            if (!columnExists)
            {
                using (var alterCommand = connection.CreateCommand())
                {
                    alterCommand.CommandText = "ALTER TABLE UserProgress ADD COLUMN NextReview TEXT";
                    alterCommand.ExecuteNonQuery();
                }
            }

            using (var migrateCommand = connection.CreateCommand())
            {
                migrateCommand.CommandText = @"
                    UPDATE UserProgress
                    SET NextReview = strftime(
                        '%Y-%m-%dT%H:%M:%fZ',
                        LastAnswer,
                        printf(
                            '+%d days',
                            CASE
                                WHEN COALESCE(Proficiency, 0) <= 0 THEN 1
                                WHEN Proficiency = 1 THEN 2
                                WHEN Proficiency = 2 THEN 4
                                WHEN Proficiency = 3 THEN 8
                                WHEN Proficiency = 4 THEN 16
                                ELSE 32
                            END
                        )
                    )
                    WHERE (NextReview IS NULL OR NextReview = '')
                      AND LastAnswer IS NOT NULL
                      AND LastAnswer <> ''";
                migrateCommand.ExecuteNonQuery();
            }
        }
    }

    public void CreateDB()
    {
        using (var connection = new SqliteConnection(connectString))
        {
            connection.Open();
            connection.Close();
        }
    }
    public DataTable ReadFromDB(string tableName, int? id = null)
    {
        //Create the db connection
        using (SqliteConnection connection = new SqliteConnection(connectString))
        {
            connection.Open();
            //Set up command to allow db control
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM " + tableName;
                //If indicate id
                if (id != null)
                {
                    //Parameterized query
                    command.CommandText += " WHERE Id=@id";
                    command.Parameters.AddWithValue("@id", id.Value);
                }
                return GetTableFromDataReader(command);
            }
        }
    }

    public DataTable GetTableFromSQLcommand(string sqlCommand)
    {
        using (SqliteConnection connection = new SqliteConnection(connectString))
        {
            connection.Open();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sqlCommand;
                return GetTableFromDataReader(command);
            }
        }
    }

    private DataTable GetTableFromDataReader(SqliteCommand command)
    {
        //Iterate over the data returned
        using (IDataReader reader = command.ExecuteReader())
        {
            DataTable table = new DataTable();
            //Add columns dynamically
            for (int i = 0; i < reader.FieldCount; i++)
            {
                DataColumn column = new DataColumn(reader.GetName(i));
                table.Columns.Add(column);
            }
            while (reader.Read())
            {
                DataRow row = table.NewRow();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader[i];
                }
                table.Rows.Add(row);
            }
            reader.Close();
            return table;
        }
    }
    public static void PrintDataTable(DataTable table)
    {
        string columnName = "";
        // Print column name
        foreach (DataColumn column in table.Columns)
        {
            columnName += column.ColumnName + "\t";
        }
        Debug.Log(columnName);
        //Print row items
        foreach (DataRow row in table.Rows)
        {
            string items = "";
            foreach (var item in row.ItemArray)
            {
                if (item == DBNull.Value)

                    items += "NULL\t";
                else
                    items += item.ToString() + "\t";
            }
            Debug.Log(items);
        }
    }

    public void InsertIntoDB(string tableName, Dictionary<string, object> data)
    {
        using (var connection = new SqliteConnection(connectString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                // 動態生成 SQL INSERT 語法，例如：INSERT INTO tableName (Col1, Col2) VALUES (@Col1, @Col2)
                string columns = string.Join(", ", data.Keys);
                string parameters = string.Join(", ", data.Keys.Select(key => "@" + key));
                command.CommandText = $"INSERT OR REPLACE INTO {tableName} ({columns}) VALUES ({parameters})";

                // 加入參數並處理可能的 null 值
                foreach (var pair in data)
                {
                    command.Parameters.AddWithValue("@" + pair.Key, pair.Value ?? DBNull.Value);
                }

                // 執行命令
                command.ExecuteNonQuery();
            }
        }
    }
}
