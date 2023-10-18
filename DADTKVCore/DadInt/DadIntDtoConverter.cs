namespace Dadtkv;

/// <summary>
///     Converter between <see cref="DadInt" /> and <see cref="DadIntDto" />.
/// </summary>
public static class DadIntDtoConverter
{
    /// <summary>
    ///     Convert from <see cref="DadIntDto" /> to <see cref="DadInt" />.
    /// </summary>
    /// <param name="dto">The <see cref="DadIntDto" /> to convert from.</param>
    /// <returns>The converted <see cref="DadInt" />.</returns>
    public static DadInt ConvertFromDto(DadIntDto dto)
    {
        return new DadInt(
            dto.Key,
            dto.Value
        );
    }

    /// <summary>
    ///     Convert from <see cref="DadInt" /> to <see cref="DadIntDto" />.
    /// </summary>
    /// <param name="dadInt">The <see cref="DadInt" /> to convert from.</param>
    /// <returns>The converted <see cref="DadIntDto" />.</returns>
    public static DadIntDto ConvertToDto(DadInt dadInt)
    {
        return new DadIntDto
        {
            Key = dadInt.Key,
            Value = dadInt.Value
        };
    }
}