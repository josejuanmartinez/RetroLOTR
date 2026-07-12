using System;
using System.Collections.Generic;

[Serializable]
public class NonPlayableLeaderTriviaQuestion
{
    public string prompt;
    public List<string> options = new();
    public string answer;
}

[Serializable]
public class NonPlayableLeaderTriviaEntry
{
    public string characterName;
    public List<NonPlayableLeaderTriviaQuestion> questions = new();
}

[Serializable]
public class NonPlayableLeaderTriviaCollection
{
    public List<NonPlayableLeaderTriviaEntry> leaders = new();
}
