namespace Dadtkv;

/// <summary>
///     A key-value pair with a string key and an int value.
/// </summary>
public class DadInt
{
    public readonly string Key;
    public readonly int Value;

    public DadInt(string key, int value)
    {
        Key = key;
        Value = value;
    }

    public override string ToString()
    {
        return $"DadInt(Key: {Key}, Value: {Value})";
    }
}