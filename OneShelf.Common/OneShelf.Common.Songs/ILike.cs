namespace OneShelf.Common.Songs;

public interface ILike
{
    public byte Level { get; }

    public long UserId { get; }
}