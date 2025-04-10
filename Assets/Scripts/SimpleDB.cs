using UnityEngine;
using Mono.Data.Sqlite;
using System.Data;
using System;

public class SimpleDB
{
    private string dbName = "URI=file:WordGame.db";

    public void CreateDB()
    {
        using (var connection = new SqliteConnection(dbName))
        {
            connection.Open();
            connection.Close();
        }
    }
    public DataTable ReadFromDB(string tableName, int? id = null)
    {
        //Create the db connection
        using (SqliteConnection connection = new SqliteConnection(dbName))
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
        using (SqliteConnection connection = new SqliteConnection(dbName))
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
}
