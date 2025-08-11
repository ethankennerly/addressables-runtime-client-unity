using System;

[Serializable]
public record ManifestDto
{
    public int version = 1;
    public PackDto[] packs;
}
