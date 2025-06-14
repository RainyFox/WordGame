﻿using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class UserProgress
{
    public int wordNumber;
    public int proficiency;
    public string lastAnswerTimestamp;
    public int totalCorrect;
    public int totalWrong;
    public string mode;
    public UserProgress(DataRow row)
    {
        wordNumber = int.Parse(row["番号"].ToString());
        proficiency = int.Parse(row["proficiency"].ToString());
        lastAnswerTimestamp = row["LastAnswer"].ToString();
        totalCorrect = int.Parse(row["TotalCorrect"].ToString());
        totalWrong = int.Parse(row["TotalWrong"].ToString());
        mode = row["Mode"].ToString();
    }
    public UserProgress(int wordNumber, string mode)
    {
        this.wordNumber = wordNumber;
        proficiency = 0;
        totalCorrect = 0;
        totalWrong = 0;
        this.mode = mode;
    }
    public void OnAnswer(bool isCorrect)
    {
        if (isCorrect)
        {
            proficiency = Mathf.Min(proficiency + 1, 5);
            totalCorrect += 1;
        }
        else
        {
            proficiency = Mathf.Max(proficiency - 1, 0);
            totalWrong += 1;
        }
        lastAnswerTimestamp = DateTime.UtcNow.ToString("o");
    }

    public Dictionary<string, object> ToDictionary()
    {
        var data = new Dictionary<string, object>
        {
            { "番号", wordNumber },
            { "proficiency", proficiency },
            { "LastAnswer", lastAnswerTimestamp },
            { "TotalCorrect", totalCorrect },
            { "TotalWrong", totalWrong },
            { "Mode", mode  }
        };
        return data;
    }
}
