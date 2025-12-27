using System;
using System.Collections.Generic;

[Serializable]
public class RiddleCollection
{
    public List<Riddle> riddles = new();
}

[Serializable]
public class Riddle
{
    public string prompt;
    public List<string> options = new();
    public string answer;
}
