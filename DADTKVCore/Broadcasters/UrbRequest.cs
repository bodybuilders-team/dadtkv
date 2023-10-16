namespace DADTKVTransactionManager;

public interface IUrbRequest<in TR>
{
    public ulong SequenceNum { get; set; }
    public ulong MessageId { get; }
}