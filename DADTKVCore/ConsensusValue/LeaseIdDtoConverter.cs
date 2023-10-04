namespace DADTKV;

public static class LeaseIdDtoConverter
{
    public static LeaseId ConvertFromDto(LeaseIdDto leaseIdDto)
    {
        return new LeaseId
        {
            ServerId = leaseIdDto.ServerId,
            SequenceNum = leaseIdDto.SequenceNum
        };
    }

    public static LeaseIdDto ConvertToDto(LeaseId leaseId)
    {
        return new LeaseIdDto
        {
            ServerId = leaseId.ServerId,
            SequenceNum = leaseId.SequenceNum
        }; 
    }
}