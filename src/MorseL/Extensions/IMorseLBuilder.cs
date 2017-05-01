using Microsoft.Extensions.DependencyInjection;

namespace MorseL.Extensions
{
    public interface IMorseLBuilder
    {
        IServiceCollection Services { get; }
    }
}
