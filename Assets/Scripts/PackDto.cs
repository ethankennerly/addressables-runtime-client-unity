using System;

[Serializable]
public record PackDto
{
    public string id;
    public string title;
    public string releaseUtc;
    public string catalogFile;
}
