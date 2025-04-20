using UnityEngine;

public class GameMode
{
    enum WordType
    {
        NORMAL,TEXTBOOK,NONTEXTBOOK
    }
    enum Mode
    {
        RANDOM, PROFICIENCY
    }
    WordType wordType;
    Mode mode;
}
