namespace DADTKV;

public interface ITobRequest<in TR> : IUrbRequest<TR>
{
    public ulong TobMessageId { get; set; }
}