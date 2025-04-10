using System.Data;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    SimpleDB db = new();
    void Start()
    {
        int from = 1;
        int to = 43;
        //db.CreateDB();
        var table = ReadWordsInRange(from, to, 5);
        SimpleDB.PrintDataTable(table);

    }
    DataTable ReadWordsInRange(int from, int to, int quantity)
    {
        // TODO: Write SQL query command
        string command = $@"
            SELECT * 
            FROM Vocabulary
            WHERE 番号 between {from} and {to}
            ORDER by random()
            limit {quantity}";

        DataTable wordsInRange = db.GetTableFromSQLcommand(command);
        return wordsInRange;
    }
}
