namespace Dadtkv.Requests.TxSubmitRequest;

public static class TxSubmitRequestDtoConverter
{
    public static TxSubmitRequest ConvertFromDto(TxSubmitRequestDto txSubmitRequestDto)
    {
        return new TxSubmitRequest(
            clientId: txSubmitRequestDto.ClientID,
            readSet: txSubmitRequestDto.ReadSet.ToList(),
            writeSet: txSubmitRequestDto.WriteSet.Select(DadIntDtoConverter.ConvertFromDto).ToList()
        );
    }

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