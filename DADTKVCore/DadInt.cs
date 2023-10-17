namespace Dadtkv;

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