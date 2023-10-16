namespace DADTKV;

public interface ITobRequest<in TR> : IFifoUrbRequest<TR> where TR : IUrbRequest<TR>
{
}