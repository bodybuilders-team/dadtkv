namespace Dadtkv;

/// <summary>
///     Converter between <see cref="TxSubmitRequest" /> and <see cref="TxSubmitRequestDto" />.
/// </summary>
public static class TxSubmitRequestDtoConverter
{
    /// <summary>
    ///     Convert from <see cref="TxSubmitRequestDto" /> to <see cref="TxSubmitRequest" />.
    /// </summary>
    /// <param name="txSubmitRequestDto">The <see cref="TxSubmitRequestDto" /> to convert from.</param>
    /// <returns>The converted <see cref="TxSubmitRequest" />.</returns>
    public static TxSubmitRequest ConvertFromDto(TxSubmitRequestDto txSubmitRequestDto)
    {
        return new TxSubmitRequest(
            txSubmitRequestDto.ClientID,
            txSubmitRequestDto.ReadSet.ToList(),
            txSubmitRequestDto.WriteSet.Select(DadIntDtoConverter.ConvertFromDto).ToList()
        );
    }

    /// <summary>
    ///     Convert from <see cref="TxSubmitRequest" /> to <see cref="TxSubmitRequestDto" />.
    /// </summary>
    /// <param name="txSubmitRequest">The <see cref="TxSubmitRequest" /> to convert from.</param>
    /// <returns>The converted <see cref="TxSubmitRequestDto" />.</returns>
    public static TxSubmitRequestDto ConvertToDto(TxSubmitRequest txSubmitRequest)
    {
        return new TxSubmitRequestDto
        {
            ClientID = txSubmitRequest.ClientId,
            ReadSet = { txSubmitRequest.ReadSet },
            WriteSet = { txSubmitRequest.WriteSet.Select(DadIntDtoConverter.ConvertToDto).ToList() }
        };
    }
}