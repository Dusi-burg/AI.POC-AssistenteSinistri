using Dusiburg.AI.Sinistri.Ai.Ollama;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Health;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Ai;

public static class AiServiceCollectionExtensions
{
    /// <summary>Client della chat e generatore di embedding, creati dalle manopole (D12), e probe di Ollama.</summary>
    public static IServiceCollection AddSinistriAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<EmbeddingGeneratorFactory>();
        services.AddSingleton(provider => provider.GetRequiredService<ChatClientFactory>().Create());
        services.AddSingleton(provider => provider.GetRequiredService<EmbeddingGeneratorFactory>().Create());
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<IHealthProbe, OllamaHealthProbe>();

        return services;
    }
}
