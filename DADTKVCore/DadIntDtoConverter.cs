namespace Dadtkv;

public class DadIntDtoConverter
{
    public static DadInt ConvertFromDto(DadIntDto dto)
    {
        return new DadInt(
            key: dto.Key,
            value: dto.Value
        );
    }
    
    public static DadIntDto ConvertToDto(DadInt dadInt)
    {
        return new DadIntDto
        {
            Key = dadInt.Key,
            Value = dadInt.Value
        };
    }
}