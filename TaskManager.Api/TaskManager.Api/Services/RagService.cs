using OpenAI.Embeddings;
using Pinecone;
using Microsoft.Extensions.Configuration;



namespace TaskManager.Api.Services
{
    public class RagService
    {
      
        private readonly PineconeClient _pinecone;
        private readonly EmbeddingClient _embeddings;
        private readonly string _indexHost;

        public RagService(IConfiguration configuration)
        {
          
            _pinecone = new PineconeClient(configuration["Pinecone:ApiKey"]!);
            _embeddings = new EmbeddingClient(
                "text-embedding-3-small",
                configuration["OpenAI:ApiKey"]!
                );

            _indexHost = configuration["Pinecone:Host"]!;
        }



        // generate embedding for a given text

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var result = await _embeddings.GenerateEmbeddingAsync(text);
            return result.Value.ToFloats().ToArray();
        }


        // save task embedding to Pinecone vector database

        public async Task UpsertTaskAsync( int taskId, string title, string? description)
        {
            var text = $"{title} {description}";
            var embedding = await GetEmbeddingAsync(text);
            var index = _pinecone.Index(host: _indexHost);

            await index.UpsertAsync(new UpsertRequest
            {
                Vectors = new List<Vector>
                {
                    new Vector
                    {
                        Id = taskId.ToString(),
                        Values = embedding,
                        Metadata = new Metadata
                        {
                            { "title", title },
                            { "description", description ?? string.Empty }
                        }
                    }
                }
            });

        }


        public async Task<List<(int Id, String Title)>> SearchSimilarAsync(string query, int topK = 3)
        {
            var embedding = await GetEmbeddingAsync(query);
            var index = _pinecone.Index(host: _indexHost);
            var results = await index.QueryAsync(new QueryRequest
            {
                Vector = embedding,
                TopK = (uint)topK,
                IncludeMetadata = true
            });

            return results.Matches?
                .Where(m=> m.Score >= 0,5f && m.Metadata != null)
                .Select(m => (Id: int.Parse(m.Id), Title: m.Metadata["title"].ToString() ?? ""))
                .ToList() ?? new List<(int, string)>();
        }

    }
}
