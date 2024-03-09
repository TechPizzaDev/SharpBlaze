namespace SharpBlaze;

public interface IConstructible<T, TArgs> 
    where T : IConstructible<T, TArgs>
{
    static abstract void Construct(ref T instance, in TArgs args);
}
