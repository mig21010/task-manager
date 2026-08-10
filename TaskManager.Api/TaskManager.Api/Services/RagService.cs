using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;
using Pinecone;
using System.Diagnostics;



namespace TaskManager.Api.Services
{
    public class RagService
    {
      
        private readonly PineconeClient _pinecone;
        private readonly EmbeddingClient _embeddings;
        private readonly string _indexHost;

        public RagService(IConfiguration configuration)
        {

            try
            {
                Console.WriteLine("Initializing PineconeClient...");
                _pinecone = new PineconeClient(configuration["Pinecone:ApiKey"]!);

                Console.WriteLine("PineconeClient OK ✅");

                Console.WriteLine("Initializing EmbeddingClient...");
                _embeddings = new EmbeddingClient(
                "text-embedding-3-small",
                configuration["OpenAI:ApiKey"]!
                );
                Console.WriteLine("EmbeddingClient OK ✅");

                Console.WriteLine("Reading Pinecone Host...");

                _indexHost = configuration["Pinecone:Host"]!;

                Console.WriteLine($"Host: {_indexHost} ✅");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing RagService: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }



        // generate embedding for a given text

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            Console.WriteLine($"OpenAI Key: {_embeddings != null}");

            try
            {


                var result = await _embeddings.GenerateEmbeddingAsync(text);
                return result.Value.ToFloats().ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating embedding: {ex.Message}");
                return Array.Empty<float>();

            }
        }


        // save task embedding to Pinecone vector database

        public async Task UpsertTaskAsync( int taskId, string title, string? description)
        {

            try
            {
                Trace.WriteLine("=== START UPSERT ===");
                Trace.WriteLine($"Generating embedding for: {title}");
                var text = $"{title} {description}";
                Trace.WriteLine($"Generating embedding...");

                var embedding = await GetEmbeddingAsync(text);

                Trace.WriteLine($"Embedding: {embedding.Length} dims");

                Console.WriteLine($"Getting index: {_indexHost}");
                var index = _pinecone.Index(host: _indexHost);

                Console.WriteLine("Index obtained");

                

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

                Console.WriteLine($"Task {taskId} upserted to Pinecone ✅");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RAG Error type: {ex.GetType().Name}");
                Console.WriteLine($"RAG Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
               
            }


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
                .Where(m=> m.Score >= 0.3f && m.Metadata != null)
                .Select(m => (Id: int.Parse(m.Id), Title: m.Metadata["title"].ToString() ?? ""))
                .ToList() ?? new List<(int, string)>();
        }

    }
}
