namespace DADTKVTransactionManager;

public interface IFifoUrbRequest<in TR> : IUrbRequest<TR>, IComparable<TR> where TR : IUrbRequest<TR>
{
    public ulong ServerId { get; }
}